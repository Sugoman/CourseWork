using LearningAPI.Features.Dictionaries.Queries.GetDictionaries;
using LearningTrainer.Context;
using LearningTrainerShared.Models;
using LearningTrainerShared.Constants;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using System.Security.Claims;

namespace LearningAPI.Controllers
{
    [ApiController]
    [Route("api/dictionaries")]
    public class DictionaryController : BaseApiController
    {
        private readonly ApiDbContext _context; 
        private readonly IMediator _mediator;
        private readonly ILogger<DictionaryController> _logger;

        // Конструктор принимает ОБА параметра
        public DictionaryController(ApiDbContext context, IMediator mediator, ILogger<DictionaryController> logger)
        {
            _context = context;
            _mediator = mediator;
            _logger = logger;
        }

        // GET: /api/dictionaries
        [HttpGet]
        public async Task<IActionResult> GetDictionaries(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string orderBy = "Id",
            [FromQuery] bool descending = true)
        {
            const int maxPageSize = 100;
            if (pageSize > maxPageSize)
                pageSize = maxPageSize;

            if (page < 1)
                page = 1;

            var userId = GetUserId();

            var query = _context.Dictionaries
                .Where(d => d.UserId == userId)
                .Include(d => d.Words)
                .AsNoTracking();

            // Сортировка
            query = orderBy switch
            {
                "Name" => descending ? query.OrderByDescending(d => d.Name) : query.OrderBy(d => d.Name),
                "Id" => descending ? query.OrderByDescending(d => d.Id) : query.OrderBy(d => d.Id),
                _ => query.OrderByDescending(d => d.Id)
            };

            var total = await query.CountAsync();
            var dictionaries = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            Response.Headers.Add("X-Total-Count", total.ToString());
            Response.Headers.Add("X-Page-Size", pageSize.ToString());

            return Ok(new
            {
                data = dictionaries,
                pagination = new
                {
                    page,
                    pageSize,
                    total,
                    pageCount = (int)Math.Ceiling(total / (double)pageSize)
                }
            });
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetDictionaryById(int id)
        {
            var userId = GetUserId();

            var dictionary = await _context.Dictionaries
                 .Include(d => d.Words)
                 .FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId);

            if (dictionary == null) return NotFound();
            return Ok(dictionary);
        }

        [HttpGet("list/available")]
        public async Task<ActionResult<List<Dictionary>>> GetAvailableDictionaries()
        {
            var currentUserId = GetUserId();

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

        // POST: /api/dictionaries - только для учителей и админов
        [HttpPost]
        [Authorize(Roles = $"{UserRoles.Teacher},{UserRoles.Admin}")]
        public async Task<IActionResult> AddDictionary([FromBody] CreateDictionaryRequest requestDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var userId = GetUserId();

                _logger.LogInformation("Creating dictionary: Name={DictionaryName}, LanguageFrom={From}, LanguageTo={To}, UserId={UserId}",
                    requestDto.Name, requestDto.LanguageFrom, requestDto.LanguageTo, userId);

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

                _logger.LogInformation("Dictionary created successfully with ID {DictionaryId}", newDictionary.Id);

                return CreatedAtAction(nameof(GetDictionaries), new { id = newDictionary.Id }, newDictionary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating dictionary for user {UserId}", GetUserId());
                throw;
            }
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteDictionary(int id)
        {
            var userId = GetUserId();

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