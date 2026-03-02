using LearningAPI.Extensions;
using LearningTrainerShared.Context;
using LearningTrainerShared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace LearningAPI.Services
{
    public class DictionaryService : IDictionaryService
    {
        private readonly ApiDbContext _context;
        private readonly IDistributedCache _cache;

        public DictionaryService(ApiDbContext context, IDistributedCache cache)
        {
            _context = context;
            _cache = cache;
        }

        public async Task<(List<DictionaryListItemDto> Data, int Total)> GetDictionariesPagedAsync(
            int userId, int page, int pageSize, string orderBy, bool descending, CancellationToken ct = default)
        {
            // sharedIds как подзапрос вместо отдельного roundtrip
            var query = _context.Dictionaries
                .Where(d => d.UserId == userId ||
                       _context.DictionarySharings
                           .Where(ds => ds.StudentId == userId)
                           .Select(ds => ds.DictionaryId)
                           .Contains(d.Id))
                .AsNoTracking();

            query = orderBy switch
            {
                "Name" => descending ? query.OrderByDescending(d => d.Name) : query.OrderBy(d => d.Name),
                "Id" => descending ? query.OrderByDescending(d => d.Id) : query.OrderBy(d => d.Id),
                _ => query.OrderByDescending(d => d.Id)
            };

            var total = await query.CountAsync(ct);

            // Проекция напрямую в DTO — без хака с фиктивными Word объектами
            var data = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(d => new DictionaryListItemDto
                {
                    Id = d.Id,
                    UserId = d.UserId,
                    Name = d.Name,
                    Description = d.Description,
                    LanguageFrom = d.LanguageFrom,
                    LanguageTo = d.LanguageTo,
                    IsPublished = d.IsPublished,
                    Rating = d.Rating,
                    RatingCount = d.RatingCount,
                    DownloadCount = d.DownloadCount,
                    SourceDictionaryId = d.SourceDictionaryId,
                    WordCount = d.Words.Count,
                    Tags = d.Tags
                })
                .ToListAsync(ct);

            return (data, total);
        }

        public async Task<Dictionary?> GetByIdAsync(int userId, int dictionaryId, CancellationToken ct = default)
        {
            var cacheKey = $"dict:{userId}:{dictionaryId}";
            var cached = await _cache.TryGetStringAsync(cacheKey);
            if (cached != null)
                return JsonSerializer.Deserialize<Dictionary>(cached);

            var dictionary = await _context.Dictionaries
                .Include(d => d.Words)
                .FirstOrDefaultAsync(d => d.Id == dictionaryId && d.UserId == userId, ct);

            if (dictionary != null)
            {
                var json = JsonSerializer.Serialize(dictionary);
                await _cache.TrySetStringAsync(cacheKey, json, new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
                });
            }

            return dictionary;
        }

        public async Task<List<Dictionary>> GetAvailableAsync(int userId, CancellationToken ct = default)
        {
            // Один запрос с подзапросом вместо двух roundtrip'ов
            return await _context.Dictionaries
                .Include(d => d.Words)
                .Where(d => d.UserId == userId ||
                       _context.DictionarySharings
                           .Where(ds => ds.StudentId == userId)
                           .Select(ds => ds.DictionaryId)
                           .Contains(d.Id))
                .ToListAsync(ct);
        }

        public async Task<Dictionary> CreateAsync(int userId, CreateDictionaryRequest request, CancellationToken ct = default)
        {
            var newDictionary = new Dictionary
            {
                Name = request.Name,
                Description = request.Description,
                LanguageFrom = request.LanguageFrom,
                LanguageTo = request.LanguageTo,
                Tags = request.Tags,
                Words = new List<Word>(),
                UserId = userId
            };

            _context.Dictionaries.Add(newDictionary);
            await _context.SaveChangesAsync(ct);
            await InvalidateCacheAsync(userId, ct);

            return newDictionary;
        }

        public async Task<bool> UpdateAsync(int userId, int dictionaryId, UpdateDictionaryRequest request, CancellationToken ct = default)
        {
            var existing = await _context.Dictionaries
                .FirstOrDefaultAsync(d => d.Id == dictionaryId, ct);

            if (existing == null || existing.UserId != userId)
                return false;

            existing.Name = request.Name;
            existing.Description = request.Description ?? existing.Description;
            existing.LanguageFrom = request.LanguageFrom;
            existing.LanguageTo = request.LanguageTo;
            existing.Tags = request.Tags ?? existing.Tags;

            await _context.SaveChangesAsync(ct);
            await _cache.TryRemoveAsync($"dict:{userId}:{dictionaryId}");

            return true;
        }

        public async Task<bool> DeleteAsync(int userId, int dictionaryId, CancellationToken ct = default)
        {
            var dictionary = await _context.Dictionaries
                .Include(d => d.Words).ThenInclude(w => w.Progress)
                .FirstOrDefaultAsync(d => d.Id == dictionaryId, ct);

            if (dictionary == null) return false;
            if (dictionary.UserId != userId) return false;

            if (dictionary.Words.Any())
            {
                var wordIds = dictionary.Words.Select(w => w.Id).ToList();
                var progresses = _context.LearningProgresses.Where(p => wordIds.Contains(p.WordId));
                _context.LearningProgresses.RemoveRange(progresses);
                _context.Words.RemoveRange(dictionary.Words);
            }

            _context.Dictionaries.Remove(dictionary);
            await _context.SaveChangesAsync(ct);
            await _cache.TryRemoveAsync($"dict:{userId}:{dictionaryId}");

            return true;
        }

        public async Task<List<Word>> GetReviewSessionAsync(int userId, int dictionaryId, CancellationToken ct = default)
        {
            var currentUser = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
            var teacherId = currentUser?.UserId;
            var now = DateTime.UtcNow;

            var allWordsAndDates = await _context.Words
                .Where(w => w.DictionaryId == dictionaryId &&
                       (w.UserId == userId || (teacherId != null && w.UserId == teacherId)))
                .Select(w => new
                {
                    TheWord = w,
                    ReviewDate = w.Progress
                        .Where(p => p.UserId == userId)
                        .Select(p => (DateTime?)p.NextReview)
                        .FirstOrDefault()
                })
                .ToListAsync(ct);

            return allWordsAndDates
                .Where(x => !x.ReviewDate.HasValue || x.ReviewDate.Value <= now)
                .Select(x => x.TheWord)
                .OrderBy(_ => Guid.NewGuid())
                .ToList();
        }

        private async Task InvalidateCacheAsync(int userId, CancellationToken ct = default)
        {
            var dictionaryIds = await _context.Dictionaries
                .Where(d => d.UserId == userId)
                .Select(d => d.Id)
                .ToListAsync(ct);

            var tasks = dictionaryIds.Select(id => _cache.TryRemoveAsync($"dict:{userId}:{id}"));
            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// Получить все уникальные теги из словарей пользователя (#9 LEARNING_IMPROVEMENTS)
        /// </summary>
        public async Task<List<string>> GetAllTagsAsync(int userId, CancellationToken ct = default)
        {
            var rawTags = await _context.Dictionaries
                .Where(d => d.UserId == userId && d.Tags != null && d.Tags != "")
                .Select(d => d.Tags!)
                .ToListAsync(ct);

            return rawTags
                .SelectMany(t => t.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(t => t)
                .ToList();
        }
    }
}
