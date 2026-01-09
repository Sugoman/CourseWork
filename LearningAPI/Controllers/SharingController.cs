using LearningTrainer.Context;
using LearningTrainerShared.Models;
using LearningTrainerShared.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace LearningAPI.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/sharing")]
    public class SharingController : BaseApiController
    {
        private readonly ApiDbContext _context;
        private readonly ILogger<SharingController> _logger;

        public SharingController(ApiDbContext context, ILogger<SharingController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet("dictionary/{dictionaryId}/status")]
        public async Task<IActionResult> GetDictionarySharingStatus(int dictionaryId)
        {
            var teacherId = GetUserId();

            var dictionaryExists = await _context.Dictionaries
                .AnyAsync(d => d.Id == dictionaryId && d.UserId == teacherId);

            if (!dictionaryExists)
            {
                return NotFound();
            }

            var sharedStudentIds = await _context.DictionarySharings
                .Where(ds => ds.DictionaryId == dictionaryId)
                .Select(ds => ds.StudentId)
                .ToListAsync();

            return Ok(sharedStudentIds); 
        }

        [HttpPost("dictionary/toggle")]
        [Authorize(Roles = UserRoles.Teacher)]  // Только учителя могут делиться словарями
        public async Task<IActionResult> ToggleDictionarySharing([FromBody] ToggleSharingRequest request)
        {
            try
            {
                var teacherId = GetUserId();

                _logger.LogInformation("Toggle dictionary sharing: DictionaryId={DictionaryId}, StudentId={StudentId}, TeacherId={TeacherId}",
                    request.ContentId, request.StudentId, teacherId);

                var dictionary = await _context.Dictionaries
                    .FirstOrDefaultAsync(d => d.Id == request.ContentId && d.UserId == teacherId);

                var student = await _context.Users
                    .FirstOrDefaultAsync(u => u.Id == request.StudentId && u.UserId == teacherId);

                if (dictionary == null || student == null)
                {
                    _logger.LogWarning("Unauthorized action: AccessDictionary by user {UserId}, Dictionary={DictionaryId}",
                        teacherId, request.ContentId);
                    return NotFound();
                }

                var sharingEntry = await _context.DictionarySharings
                    .FirstOrDefaultAsync(ds =>
                        ds.DictionaryId == request.ContentId &&
                        ds.StudentId == request.StudentId);

                if (sharingEntry == null)
                {
                    var newEntry = new DictionarySharing
                    {
                        DictionaryId = request.ContentId,
                        StudentId = request.StudentId,
                        SharedAt = DateTime.UtcNow
                    };
                    _context.DictionarySharings.Add(newEntry);
                    try
                    {
                        await _context.SaveChangesAsync();
                        _logger.LogInformation("Dictionary shared successfully: DictionaryId={DictionaryId}, StudentId={StudentId}",
                            request.ContentId, request.StudentId);
                        return Ok(new { Message = "Доступ предоставлен", Status = "Shared" });
                    }
                    catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("PRIMARY KEY") ?? false)
                    {
                        _logger.LogWarning("Race condition caught: Dictionary already shared", request.ContentId, request.StudentId);
                        return Conflict(new { Message = "Словарь уже распределён этому студенту" });
                    }
                }
                else
                {
                    _context.DictionarySharings.Remove(sharingEntry);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Dictionary access revoked: DictionaryId={DictionaryId}, StudentId={StudentId}",
                        request.ContentId, request.StudentId);
                    return Ok(new { Message = "Доступ отозван", Status = "Unshared" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling dictionary sharing for DictionaryId={DictionaryId}, StudentId={StudentId}",
                    request.ContentId, request.StudentId);
                throw;
            }
        }
    }

    public class ToggleSharingRequest
    {
        public int ContentId { get; set; } 
        public int StudentId { get; set; }
    }
}