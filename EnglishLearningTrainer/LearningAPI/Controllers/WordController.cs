using EnglishLearningTrainer.Context;
using EnglishLearningTrainer.Models; // Убедись, что 'Word' тут
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LearningAPI.Controllers
{
    [ApiController]
    [Route("api/words")] // -> /api/Words
    public class WordsController : ControllerBase
    {
        private readonly ApiDbContext _context;

        public WordsController(ApiDbContext context)
        {
            _context = context;
        }

        // POST: /api/Words
        // Принимаем Word, у которого WPF уже выставил DictionaryId
        [HttpPost]
        public async Task<IActionResult> AddWord([FromBody] Word word)
        {
            if (word == null || word.DictionaryId == 0)
            {
                return BadRequest("Word data or DictionaryId is missing.");
            }

            // Мы доверяем, что WPF прислал 'DictionaryId'
            _context.Words.Add(word);
            await _context.SaveChangesAsync();

            // Возвращаем созданное слово
            return Ok(word);
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