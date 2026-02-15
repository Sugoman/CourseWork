using LearningTrainerShared.Context;
using LearningTrainer.Services;
using LearningTrainerShared.Models;
using LearningAPI.Services;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Security.Claims;

namespace LearningAPI.Controllers
{
    [ApiController]
    [Route("api/words")]
    public class WordsController : ControllerBase
    {
        private readonly ApiDbContext _context;
        private readonly TranscriptionChannel _transcriptionChannel;

        public WordsController(ApiDbContext context, TranscriptionChannel transcriptionChannel)
        {
            _context = context;
            _transcriptionChannel = transcriptionChannel;
        }
        [HttpPost]
        public async Task<IActionResult> AddWord([FromBody] CreateWordRequest requestDto)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdString, out var userId))
            {
                return Unauthorized();
            }

            if (requestDto == null || requestDto.DictionaryId == 0)
            {
                return BadRequest("Word data or DictionaryId is missing.");
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
            await _context.SaveChangesAsync();

            // Отправляем задачу на получение транскрипции в фоновый воркер
            await _transcriptionChannel.Writer.WriteAsync(
                new TranscriptionRequest(newWord.Id, newWord.OriginalWord));

            return Ok(newWord);
        }

        // DELETE: /api/Words/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteWord(int id)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdString, out var userId))
            {
                return Unauthorized();
            }

            var word = await _context.Words.FindAsync(id);
            if (word == null) return NotFound();

            if (word.UserId != userId)
            {
                return Forbid();
            }

            _context.Words.Remove(word);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
