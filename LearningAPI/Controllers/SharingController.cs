using LearningTrainer.Context;
using LearningTrainerShared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace LearningAPI.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/sharing")]
    public class SharingController : ControllerBase
    {
        private readonly ApiDbContext _context;

        public SharingController(ApiDbContext context)
        {
            _context = context;
        }

        private int GetUserId() => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

        [HttpGet("dictionary/{dictionaryId}/status")]
        public async Task<IActionResult> GetDictionarySharingStatus(int dictionaryId)
        {
            var teacherId = GetUserId();

            var dictionaryExists = await _context.Dictionaries
                .AnyAsync(d => d.Id == dictionaryId && d.UserId == teacherId);

            if (!dictionaryExists)
            {
                return Forbid("Словарь не найден или не принадлежит вам.");
            }

            var sharedStudentIds = await _context.DictionarySharings
                .Where(ds => ds.DictionaryId == dictionaryId)
                .Select(ds => ds.StudentId)
                .ToListAsync();

            return Ok(sharedStudentIds); 
        }

        [HttpPost("dictionary/toggle")]
        public async Task<IActionResult> ToggleDictionarySharing([FromBody] ToggleSharingRequest request)
        {
            var teacherId = GetUserId();

            var dictionary = await _context.Dictionaries
                .FirstOrDefaultAsync(d => d.Id == request.ContentId && d.UserId == teacherId);

            var student = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == request.StudentId && u.UserId == teacherId);

            if (dictionary == null || student == null)
            {
                return Forbid("Неверный контент или ученик не найден в вашем классе.");
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
                await _context.SaveChangesAsync();
                return Ok(new { Message = "Доступ предоставлен", Status = "Shared" });
            }
            else
            {
                _context.DictionarySharings.Remove(sharingEntry);
                await _context.SaveChangesAsync();
                return Ok(new { Message = "Доступ отозван", Status = "Unshared" });
            }
        }
    }

    public class ToggleSharingRequest
    {
        public int ContentId { get; set; } 
        public int StudentId { get; set; }
    }
}