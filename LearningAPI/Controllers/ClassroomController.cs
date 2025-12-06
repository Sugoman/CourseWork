using LearningTrainer.Context;
using LearningTrainerShared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NanoidDotNet;
using System.Security.Claims;

namespace LearningAPI.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/classroom")]
    public class ClassroomController : ControllerBase
    {
        private readonly ApiDbContext _context;

        public ClassroomController(ApiDbContext context)
        {
            _context = context;
        }

        // GET /api/classroom/my-code (Только для Учителя)
        [HttpGet("my-code")]
        public async Task<IActionResult> GetMyInviteCode()
        {
            var userId = GetUserId();
            var user = await _context.Users.FindAsync(userId);

            if (string.IsNullOrEmpty(user.InviteCode))
            {
                user.InviteCode = GenerateInviteCode();
                await _context.SaveChangesAsync();
            }

            return Ok(new { Code = user.InviteCode });
        }

        // POST /api/classroom/join (Для Ученика)
        [HttpPost("join")]
        public async Task<IActionResult> JoinClass([FromBody] JoinClassRequest request)
        {
            var studentId = GetUserId();
            var student = await _context.Users.FindAsync(studentId);

            var teacher = await _context.Users.FirstOrDefaultAsync(u => u.InviteCode == request.Code);

            if (teacher == null)
                return NotFound("Teacher with this code not found.");

            if (teacher.Id == studentId)
                return BadRequest("You cannot join your own class.");

            student.UserId = teacher.Id;
            await _context.SaveChangesAsync();

            return Ok(new { Message = $"Successfully joined {teacher.Login}'s class!" });
        }

        // GET /api/classroom/students (Для Учителя)
        [HttpGet("students")]
        public async Task<IActionResult> GetMyStudents()
        {
            var teacherId = GetUserId();

            var students = await _context.Users
                .Where(u => u.UserId == teacherId)
                .Select(s => new
                {
                    s.Id,
                    s.Login,
                    // Можно добавить статистику: сколько слов выучил и т.д.
                    WordsLearned = _context.LearningProgresses.Count(p => p.UserId == s.Id && p.KnowledgeLevel > 3)
                })
                .ToListAsync();

            return Ok(students);
        }

        private int GetUserId() => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

        private string GenerateInviteCode()
        {
            var random = new Random();
            return $"TR-{Nanoid.Generate(size:6)}";
        }
    }

    public class JoinClassRequest { public string Code { get; set; } }
}
