using LearningTrainer.Context;
using LearningTrainer.Models;
using LearningTrainerShared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LearningAPI.Controllers
{
    [ApiController]
    [Route("api/dictionaries")]
    public class DictionaryController : Controller
    {
        private readonly ApiDbContext _context;
        private HttpClient _httpClient;

        public DictionaryController(ApiDbContext context)
        {
            _context = context;
        }

        // GET: /api/dictionaries
        [HttpGet]
        public async Task<IActionResult> GetDictionaries()
        {
            var dictionaries = await _context.Dictionaries
                                             .Include(d => d.Words)
                                             .ToListAsync();
            return Ok(dictionaries);
        }

        // POST: /api/dictionaries
        [HttpPost]
        public async Task<IActionResult> AddDictionary([FromBody] CreateDictionaryRequest requestDto)
        {
            if (requestDto == null || string.IsNullOrWhiteSpace(requestDto.Name))
            {
                return BadRequest("Name is required.");
            }

            var newDictionary = new Dictionary
            {
                Name = requestDto.Name,
                Description = requestDto.Description,
                LanguageFrom = requestDto.LanguageFrom,
                LanguageTo = requestDto.LanguageTo,
                Words = new List<Word>() 
            };

            _context.Dictionaries.Add(newDictionary);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetDictionaries), new { id = newDictionary.Id }, newDictionary);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetDictionaryById(int id)
        {
            var dictionary = await _context.Dictionaries
                                             .Include(d => d.Words)
                                             .FirstOrDefaultAsync(d => d.Id == id);

            if (dictionary == null)
            {
                return NotFound();
            }
            return Ok(dictionary);
        }
        // DELETE: /api/dictionaries/5 
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteDictionary(int id)
        {
            var dictionary = await _context.Dictionaries.FindAsync(id);

            if (dictionary == null)
            {
                return NotFound();
            }

            _context.Dictionaries.Remove(dictionary);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
