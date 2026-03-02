using LearningAPI.Services;
using LearningTrainerShared.Constants;
using LearningTrainerShared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LearningAPI.Controllers
{
    [ApiController]
    [Route("api/dictionaries")]
    public class DictionaryController : BaseApiController
    {
        private readonly IDictionaryService _dictionaryService;
        private readonly ILogger<DictionaryController> _logger;

        public DictionaryController(IDictionaryService dictionaryService, ILogger<DictionaryController> logger)
        {
            _dictionaryService = dictionaryService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetDictionaries(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string orderBy = "Id",
            [FromQuery] bool descending = true,
            CancellationToken ct = default)
        {
            const int maxPageSize = 100;
            if (pageSize > maxPageSize) pageSize = maxPageSize;
            if (page < 1) page = 1;

            var userId = GetUserId();
            var (data, total) = await _dictionaryService.GetDictionariesPagedAsync(userId, page, pageSize, orderBy, descending, ct);

            Response.Headers.Append("X-Total-Count", total.ToString());
            Response.Headers.Append("X-Page-Size", pageSize.ToString());

            return Ok(new
            {
                data,
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
        public async Task<IActionResult> GetDictionaryById(int id, CancellationToken ct = default)
        {
            var dictionary = await _dictionaryService.GetByIdAsync(GetUserId(), id, ct);
            if (dictionary == null) return NotFound();
            return Ok(dictionary);
        }

        [HttpGet("list/available")]
        public async Task<IActionResult> GetAvailableDictionaries(CancellationToken ct = default)
        {
            var dictionaries = await _dictionaryService.GetAvailableAsync(GetUserId(), ct);
            return Ok(dictionaries);
        }

        [HttpPost]
        [Authorize(Roles = UserRoles.ContentCreators)]
        public async Task<IActionResult> AddDictionary([FromBody] CreateDictionaryRequest requestDto, CancellationToken ct = default)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = GetUserId();

            _logger.LogInformation("Creating dictionary: Name={DictionaryName}, LanguageFrom={From}, LanguageTo={To}, UserId={UserId}",
                requestDto.Name, requestDto.LanguageFrom, requestDto.LanguageTo, userId);

            var newDictionary = await _dictionaryService.CreateAsync(userId, requestDto, ct);

            _logger.LogInformation("Dictionary created successfully with ID {DictionaryId}", newDictionary.Id);

            return CreatedAtAction(nameof(GetDictionaries), new { id = newDictionary.Id }, newDictionary);
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteDictionary(int id, CancellationToken ct = default)
        {
            var userId = GetUserId();
            var deleted = await _dictionaryService.DeleteAsync(userId, id, ct);
            if (!deleted) return NotFound();
            return NoContent();
        }

        [HttpGet("{id:int}/review")]
        public async Task<IActionResult> GetReviewSession(int id, CancellationToken ct = default)
        {
            var words = await _dictionaryService.GetReviewSessionAsync(GetUserId(), id, ct);
            return Ok(words);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateDictionary(int id, [FromBody] UpdateDictionaryRequest request, CancellationToken ct = default)
        {
            if (id != request.Id) return BadRequest("ID mismatch");

            var updated = await _dictionaryService.UpdateAsync(GetUserId(), id, request, ct);
            if (!updated) return NotFound();
            return NoContent();
        }

        /// <summary>
        /// Получить все уникальные теги из словарей пользователя (#9 LEARNING_IMPROVEMENTS)
        /// </summary>
        [HttpGet("tags")]
        public async Task<IActionResult> GetAllTags(CancellationToken ct = default)
        {
            var userId = GetUserId();
            var tags = await _dictionaryService.GetAllTagsAsync(userId, ct);
            return Ok(tags);
        }
    }
}
