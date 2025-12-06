using LearningTrainer.Context;
using LearningTrainerShared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace LearningAPI.Controllers
{
    [ApiController]
    [Route("api/dictionaries")]
    public class DictionaryController : Controller
    {
        private readonly ApiDbContext _context;

        public DictionaryController(ApiDbContext context)
        {
            _context = context;
        }

        // GET: /api/dictionaries
        [HttpGet]
        public async Task<IActionResult> GetDictionaries()
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdString, out var userId)) return Unauthorized();

            var currentUser = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId);

            var teacherId = currentUser?.UserId;

            var dictionaries = await _context.Dictionaries
                .Where(d => d.UserId == userId || (teacherId != null && d.UserId == teacherId))
                .Select(d => new
                {
                    d.Id,
                    d.UserId,
                    d.Name,
                    d.Description,
                    d.LanguageFrom,
                    d.LanguageTo,
                    d.WordCount,
                    IsReadOnly = d.UserId != userId,
                    Words = d.Words.ToList()
                })
                .ToListAsync();

            return Ok(dictionaries);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetDictionaryById(int id)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdString, out var userId)) return Unauthorized();

            var dictionary = await _context.Dictionaries
                 .Include(d => d.Words)
                 .FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId);

            if (dictionary == null) return NotFound();
            return Ok(dictionary);
        }

        [HttpGet("list/available")]
        [Authorize]
        public async Task<ActionResult<List<Dictionary>>> GetAvailableDictionaries()
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdString, out int currentUserId)) return Unauthorized();

            var sharedIds = await _context.DictionarySharings
                .Where(ds => ds.StudentId == currentUserId)
                .Select(ds => ds.DictionaryId)
                .ToListAsync();

            var dictionaries = await _context.Dictionaries
                .Include(d => d.Words)
                .Where(d => d.UserId == currentUserId || sharedIds.Contains(d.Id))
                .ToListAsync();

            return Ok(dictionaries);
        }

        // POST: /api/dictionaries
        [HttpPost]
        public async Task<IActionResult> AddDictionary([FromBody] CreateDictionaryRequest requestDto)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdString, out var userId)) return Unauthorized();

            if (requestDto == null || string.IsNullOrWhiteSpace(requestDto.Name))
                return BadRequest("Name is required.");

            var newDictionary = new Dictionary
            {
                Name = requestDto.Name,
                Description = requestDto.Description,
                LanguageFrom = requestDto.LanguageFrom,
                LanguageTo = requestDto.LanguageTo,
                Words = new List<Word>(),
                UserId = userId
            };

            _context.Dictionaries.Add(newDictionary);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetDictionaries), new { id = newDictionary.Id }, newDictionary);
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteDictionary(int id)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdString, out var userId)) return Unauthorized();

            var dictionary = await _context.Dictionaries
                .Include(d => d.Words).ThenInclude(w => w.Progress)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (dictionary == null) return NotFound();

            if (dictionary.UserId != userId) return Forbid();

            if (dictionary.Words.Any())
            {
                var wordIds = dictionary.Words.Select(w => w.Id).ToList();
                var progresses = _context.LearningProgresses.Where(p => wordIds.Contains(p.WordId));
                _context.LearningProgresses.RemoveRange(progresses);

                _context.Words.RemoveRange(dictionary.Words);
            }

            _context.Dictionaries.Remove(dictionary);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpGet("{id:int}/review")]
        public async Task<IActionResult> GetReviewSession(int id)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdString, out var userId)) return Unauthorized();

            var currentUser = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
            var teacherId = currentUser?.UserId;
            var now = DateTime.UtcNow;

            var allWordsAndDates = await _context.Words
                .Where(w => w.DictionaryId == id && (w.UserId == userId || (teacherId != null && w.UserId == teacherId)))
                .Select(w => new
                {
                    TheWord = w,
                    ReviewDate = w.Progress
                        .Where(p => p.UserId == userId)
                        .Select(p => (DateTime?)p.NextReview)
                        .FirstOrDefault()
                })
                .ToListAsync();

            var studySession = allWordsAndDates
                .Where(x => !x.ReviewDate.HasValue || x.ReviewDate.Value <= now)
                .Select(x => x.TheWord)
                .ToList();

            var shuffledSession = studySession.OrderBy(w => Guid.NewGuid()).ToList();

            return Ok(shuffledSession);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateDictionary(int id, [FromBody] Dictionary dictionary)
        {
            if (id != dictionary.Id) return BadRequest("ID mismatch");

            _context.Entry(dictionary).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Dictionaries.Any(e => e.Id == id)) return NotFound();
                else throw;
            }

            return NoContent();
        }
    }
}