using LearningTrainer.Context;
using LearningTrainer.Models;
using LearningTrainer.Services;
using LearningTrainerShared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LearningAPI.Controllers
{
    [ApiController]
    [Route("api/words")] 
    public class WordsController : ControllerBase
    {
        private readonly ApiDbContext _context;
        private readonly ExternalDictionaryService _dictionaryService;

        public WordsController(ApiDbContext context, ExternalDictionaryService dictionaryService)
        {
            _context = context;
            _dictionaryService = dictionaryService;
        }
        [HttpPost]
        public async Task<IActionResult> AddWord([FromBody] CreateWordRequest requestDto)
        {
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
                AddedAt = DateTime.UtcNow
            };

            var transcription = await _dictionaryService.GetTranscriptionAsync(newWord.OriginalWord);
            newWord.Transcription = transcription;

            _context.Words.Add(newWord);
            await _context.SaveChangesAsync();

            return Ok(newWord); 
        }

        // DELETE: /api/Words/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteWord(int id)
        {
            var word = await _context.Words.FindAsync(id);
            if (word == null)
            {
                return NotFound();
            }

            _context.Words.Remove(word);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}