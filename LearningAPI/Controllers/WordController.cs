using LearningAPI.Extensions;
using LearningTrainerShared.Context;
using LearningTrainer.Services;
using LearningTrainerShared.Models;
using LearningAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;

namespace LearningAPI.Controllers
{
    [ApiController]
    [Route("api/words")]
    public class WordsController : BaseApiController
    {
        private readonly ApiDbContext _context;
        private readonly TranscriptionChannel _transcriptionChannel;
        private readonly IDistributedCache _cache;

        public WordsController(ApiDbContext context, TranscriptionChannel transcriptionChannel, IDistributedCache cache)
        {
            _context = context;
            _transcriptionChannel = transcriptionChannel;
            _cache = cache;
        }

        [HttpPost]
        public async Task<IActionResult> AddWord([FromBody] CreateWordRequest requestDto, CancellationToken ct = default)
        {
            var userId = GetUserId();

            if (requestDto == null || requestDto.DictionaryId == 0)
            {
                return BadRequest("Word data or DictionaryId is missing.");
            }

            // Проверка на дубликат слова в словаре
            var duplicate = await _context.Words
                .AnyAsync(w => w.DictionaryId == requestDto.DictionaryId
                            && w.OriginalWord == requestDto.OriginalWord, ct);
            if (duplicate)
            {
                return Conflict($"Слово «{requestDto.OriginalWord}» уже существует в этом словаре.");
            }

            var newWord = new Word
            {
                OriginalWord = requestDto.OriginalWord,
                Translation = requestDto.Translation,
                Example = requestDto.Example,
                DictionaryId = requestDto.DictionaryId,
                AddedAt = DateTime.UtcNow,
                UserId = userId
            };

            // Сохраняем слово мгновенно БЕЗ транскрипции
            _context.Words.Add(newWord);
            await _context.SaveChangesAsync(ct);

            // Инвалидируем кэш словаря (он кэшируется вместе со словами)
            await _cache.TryRemoveAsync($"dict:{userId}:{requestDto.DictionaryId}");

            // Отправляем задачу на получение транскрипции в фоновый воркер
            await _transcriptionChannel.Writer.WriteAsync(
                new TranscriptionRequest(newWord.Id, newWord.OriginalWord), ct);

            return Ok(newWord);
        }

        // DELETE: /api/Words/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteWord(int id, CancellationToken ct = default)
        {
            var userId = GetUserId();

            var word = await _context.Words.FindAsync(new object[] { id }, ct);
            if (word == null) return NotFound();

            if (word.UserId != userId)
            {
                return Forbid();
            }

            var dictionaryId = word.DictionaryId;
            _context.Words.Remove(word);
            await _context.SaveChangesAsync(ct);

            // Инвалидируем кэш словаря
            await _cache.TryRemoveAsync($"dict:{userId}:{dictionaryId}");

            return NoContent();
        }

        // PUT: /api/words/5
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateWord(int id, [FromBody] UpdateWordRequest requestDto, CancellationToken ct = default)
        {
            var userId = GetUserId();

            var word = await _context.Words.FindAsync(new object[] { id }, ct);
            if (word == null) return NotFound();

            if (word.UserId != userId)
            {
                return Forbid();
            }

            // Проверка на дубликат (если слово изменилось)
            if (!string.Equals(word.OriginalWord, requestDto.OriginalWord, StringComparison.Ordinal))
            {
                var duplicate = await _context.Words
                    .AnyAsync(w => w.DictionaryId == word.DictionaryId
                                && w.OriginalWord == requestDto.OriginalWord
                                && w.Id != id, ct);
                if (duplicate)
                {
                    return Conflict($"Слово «{requestDto.OriginalWord}» уже существует в этом словаре.");
                }
            }

            word.OriginalWord = requestDto.OriginalWord;
            word.Translation = requestDto.Translation;
            word.Example = requestDto.Example ?? "";

            await _context.SaveChangesAsync(ct);

            // Инвалидируем кэш словаря
            await _cache.TryRemoveAsync($"dict:{userId}:{word.DictionaryId}");

            // Если слово изменилось — запрашиваем новую транскрипцию
            if (!string.Equals(word.OriginalWord, requestDto.OriginalWord, StringComparison.OrdinalIgnoreCase))
            {
                word.Transcription = null;
                await _context.SaveChangesAsync(ct);
                await _transcriptionChannel.Writer.WriteAsync(
                    new TranscriptionRequest(word.Id, word.OriginalWord), ct);
            }

            return Ok(word);
        }

        // POST: /api/words/batch
        [HttpPost("batch")]
        public async Task<IActionResult> AddWordsBatch([FromBody] List<CreateWordRequest> requestDtos, CancellationToken ct = default)
        {
            var userId = GetUserId();

            if (requestDtos == null || requestDtos.Count == 0)
            {
                return BadRequest("Список слов пуст.");
            }

            var dictionaryId = requestDtos[0].DictionaryId;
            if (dictionaryId == 0)
            {
                return BadRequest("DictionaryId is missing.");
            }

            // Получаем существующие слова для проверки дублей
            var existingWords = await _context.Words
                .Where(w => w.DictionaryId == dictionaryId)
                .Select(w => w.OriginalWord.ToLower())
                .ToListAsync(ct);

            var existingSet = new HashSet<string>(existingWords);
            var addedWords = new List<Word>();
            var skipped = 0;

            foreach (var dto in requestDtos)
            {
                if (string.IsNullOrWhiteSpace(dto.OriginalWord) || string.IsNullOrWhiteSpace(dto.Translation))
                {
                    skipped++;
                    continue;
                }

                if (existingSet.Contains(dto.OriginalWord.Trim().ToLower()))
                {
                    skipped++;
                    continue;
                }

                var newWord = new Word
                {
                    OriginalWord = dto.OriginalWord.Trim(),
                    Translation = dto.Translation.Trim(),
                    Example = dto.Example?.Trim() ?? "",
                    DictionaryId = dictionaryId,
                    AddedAt = DateTime.UtcNow,
                    UserId = userId
                };

                _context.Words.Add(newWord);
                addedWords.Add(newWord);
                existingSet.Add(dto.OriginalWord.Trim().ToLower());
            }

            if (addedWords.Count > 0)
            {
                await _context.SaveChangesAsync(ct);
                await _cache.TryRemoveAsync($"dict:{userId}:{dictionaryId}");

                // Отправляем задачи на получение транскрипции
                foreach (var word in addedWords)
                {
                    await _transcriptionChannel.Writer.WriteAsync(
                        new TranscriptionRequest(word.Id, word.OriginalWord), ct);
                }
            }

            return Ok(new { Added = addedWords.Count, Skipped = skipped, Words = addedWords });
        }
    }
}
