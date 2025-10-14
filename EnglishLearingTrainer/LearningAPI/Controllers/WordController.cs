using EnglishLearningTrainer.Context;
using EnglishLearningTrainer.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LearningAPI.Controllers
{
    [ApiController]
    [Route("api/words")] 
    public class WordsController : ControllerBase
    {
        private readonly ApiDbContext _context;

        public WordsController(ApiDbContext context)
        {
            _context = context;
        }

        // POST: /api/Words
        [HttpPost]
        public async Task<IActionResult> AddWord([FromBody] Word word)
        {
            if (word == null || word.DictionaryId == 0)
            {
                return BadRequest("Word data or DictionaryId is missing.");
            }

            _context.Words.Add(word);
            await _context.SaveChangesAsync();

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