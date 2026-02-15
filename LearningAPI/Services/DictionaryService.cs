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

        public async Task<(List<Dictionary> Data, int Total)> GetDictionariesPagedAsync(
            int userId, int page, int pageSize, string orderBy, bool descending)
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

            var total = await query.CountAsync();

            // Данные + WordCount в одном SQL: d.Words.Count → (SELECT COUNT(*) FROM Words WHERE DictionaryId = d.Id)
            var projected = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(d => new
                {
                    d.Id, d.UserId, d.Name, d.Description,
                    d.LanguageFrom, d.LanguageTo, d.IsPublished,
                    d.Rating, d.RatingCount, d.DownloadCount, d.SourceDictionaryId,
                    WordCount = d.Words.Count
                })
                .ToListAsync();

            // Маппинг обратно в Dictionary (контроллер проецирует в анонимный тип, Words не сериализуются)
            var data = projected.Select(r =>
            {
                var dict = new Dictionary
                {
                    Id = r.Id, UserId = r.UserId, Name = r.Name, Description = r.Description,
                    LanguageFrom = r.LanguageFrom, LanguageTo = r.LanguageTo,
                    IsPublished = r.IsPublished, Rating = r.Rating, RatingCount = r.RatingCount,
                    DownloadCount = r.DownloadCount, SourceDictionaryId = r.SourceDictionaryId
                };
                for (var i = 0; i < r.WordCount; i++)
                    dict.Words.Add(new Word());
                return dict;
            }).ToList();

            return (data, total);
        }

        public async Task<Dictionary?> GetByIdAsync(int userId, int dictionaryId)
        {
            var cacheKey = $"dict:{userId}:{dictionaryId}";
            var cached = await _cache.TryGetStringAsync(cacheKey);
            if (cached != null)
                return JsonSerializer.Deserialize<Dictionary>(cached);

            var dictionary = await _context.Dictionaries
                .Include(d => d.Words)
                .FirstOrDefaultAsync(d => d.Id == dictionaryId && d.UserId == userId);

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

        public async Task<List<Dictionary>> GetAvailableAsync(int userId)
        {
            var sharedIds = await _context.DictionarySharings
                .Where(ds => ds.StudentId == userId)
                .Select(ds => ds.DictionaryId)
                .ToListAsync();

            return await _context.Dictionaries
                .Include(d => d.Words)
                .Where(d => d.UserId == userId || sharedIds.Contains(d.Id))
                .ToListAsync();
        }

        public async Task<Dictionary> CreateAsync(int userId, CreateDictionaryRequest request)
        {
            var newDictionary = new Dictionary
            {
                Name = request.Name,
                Description = request.Description,
                LanguageFrom = request.LanguageFrom,
                LanguageTo = request.LanguageTo,
                Words = new List<Word>(),
                UserId = userId
            };

            _context.Dictionaries.Add(newDictionary);
            await _context.SaveChangesAsync();
            await InvalidateCacheAsync(userId);

            return newDictionary;
        }

        public async Task<bool> UpdateAsync(int userId, int dictionaryId, UpdateDictionaryRequest request)
        {
            var existing = await _context.Dictionaries
                .FirstOrDefaultAsync(d => d.Id == dictionaryId);

            if (existing == null || existing.UserId != userId)
                return false;

            existing.Name = request.Name;
            existing.Description = request.Description ?? existing.Description;
            existing.LanguageFrom = request.LanguageFrom;
            existing.LanguageTo = request.LanguageTo;

            await _context.SaveChangesAsync();
            await _cache.TryRemoveAsync($"dict:{userId}:{dictionaryId}");

            return true;
        }

        public async Task<bool> DeleteAsync(int userId, int dictionaryId)
        {
            var dictionary = await _context.Dictionaries
                .Include(d => d.Words).ThenInclude(w => w.Progress)
                .FirstOrDefaultAsync(d => d.Id == dictionaryId);

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
            await _context.SaveChangesAsync();
            await _cache.TryRemoveAsync($"dict:{userId}:{dictionaryId}");

            return true;
        }

        public async Task<List<Word>> GetReviewSessionAsync(int userId, int dictionaryId)
        {
            var currentUser = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
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
                .ToListAsync();

            return allWordsAndDates
                .Where(x => !x.ReviewDate.HasValue || x.ReviewDate.Value <= now)
                .Select(x => x.TheWord)
                .OrderBy(_ => Guid.NewGuid())
                .ToList();
        }

        private async Task InvalidateCacheAsync(int userId)
        {
            var dictionaryIds = await _context.Dictionaries
                .Where(d => d.UserId == userId)
                .Select(d => d.Id)
                .ToListAsync();

            var tasks = dictionaryIds.Select(id => _cache.TryRemoveAsync($"dict:{userId}:{id}"));
            await Task.WhenAll(tasks);
        }
    }
}
