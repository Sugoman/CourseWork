using LearningTrainerShared.Context;
using LearningTrainerShared.Models;
using LearningTrainerShared.Services;
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
        private readonly TokenService _tokenService;

        public ClassroomController(ApiDbContext context, TokenService tokenService)
        {
            _context = context;
            _tokenService = tokenService;
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
            var student = await _context.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == studentId);

            var teacher = await _context.Users.FirstOrDefaultAsync(u => u.InviteCode == request.Code);

            if (teacher == null)
                return NotFound("Teacher with this code not found.");

            if (teacher.Id == studentId)
                return BadRequest("You cannot join your own class.");

            // Link student to teacher
            student.UserId = teacher.Id;

            // Change role to Student if currently User
            if (student.Role?.Name == "User")
            {
                var studentRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Student");
                if (studentRole != null)
                {
                    student.RoleId = studentRole.Id;
                    student.Role = studentRole;
                }
            }

            await _context.SaveChangesAsync();

            // Generate new access token with updated role
            var newAccessToken = _tokenService.GenerateAccessToken(student);

            return Ok(new
            {
                Message = $"Вы присоединились к классу {teacher.Username}!",
                AccessToken = newAccessToken,
                UserRole = student.Role?.Name ?? "Student"
            });
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
                    s.Username,
                    s.Email,
                    WordsLearned = _context.LearningProgresses.Count(p => p.UserId == s.Id && p.KnowledgeLevel > 3),
                    TotalWords = _context.LearningProgresses.Count(p => p.UserId == s.Id),
                    CurrentStreak = _context.UserStats
                        .Where(us => us.UserId == s.Id)
                        .Select(us => us.CurrentStreak)
                        .FirstOrDefault(),
                    LastPracticeDate = _context.UserStats
                        .Where(us => us.UserId == s.Id)
                        .Select(us => us.LastPracticeDate)
                        .FirstOrDefault(),
                    CorrectAnswers = _context.LearningProgresses
                        .Where(p => p.UserId == s.Id)
                        .Sum(p => p.CorrectAnswers),
                    TotalAttempts = _context.LearningProgresses
                        .Where(p => p.UserId == s.Id)
                        .Sum(p => p.TotalAttempts),
                    SharedDictionariesCount = _context.DictionarySharings
                        .Count(ds => ds.StudentId == s.Id && ds.Dictionary.UserId == teacherId),
                    SharedRulesCount = _context.RuleSharings
                        .Count(rs => rs.StudentId == s.Id && rs.Rule.UserId == teacherId)
                })
                .ToListAsync();

            return Ok(students);
        }

        // POST /api/classroom/leave (Ученик выходит из класса)
        [HttpPost("leave")]
        public async Task<IActionResult> LeaveClass()
        {
            var userId = GetUserId();
            var student = await _context.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == userId);
            if (student == null) return NotFound();

            if (student.UserId == null)
                return BadRequest(new { Message = "Вы не состоите в классе." });

            student.UserId = null;

            // Revert role from Student back to User
            if (student.Role?.Name == "Student")
            {
                var userRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "User");
                if (userRole != null)
                {
                    student.RoleId = userRole.Id;
                    student.Role = userRole;
                }
            }

            await _context.SaveChangesAsync();

            var newAccessToken = _tokenService.GenerateAccessToken(student);

            return Ok(new
            {
                Message = "Вы вышли из класса.",
                AccessToken = newAccessToken,
                UserRole = student.Role?.Name ?? "User"
            });
        }

        // POST /api/classroom/kick/{studentId} (Учитель удаляет ученика)
        [HttpPost("kick/{studentId}")]
        public async Task<IActionResult> KickStudent(int studentId)
        {
            var teacherId = GetUserId();
            var student = await _context.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == studentId);

            if (student == null)
                return NotFound(new { Message = "Ученик не найден." });

            if (student.UserId != teacherId)
                return BadRequest(new { Message = "Этот ученик не в вашем классе." });

            student.UserId = null;

            if (student.Role?.Name == "Student")
            {
                var userRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "User");
                if (userRole != null)
                {
                    student.RoleId = userRole.Id;
                    student.Role = userRole;
                }
            }

            await _context.SaveChangesAsync();

            return Ok(new { Message = $"Ученик {student.Username} удалён из класса." });
        }

        // GET /api/classroom/my-teacher (Ученик получает информацию об учителе)
        [HttpGet("my-teacher")]
        public async Task<IActionResult> GetMyTeacher()
        {
            var userId = GetUserId();
            var student = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (student?.UserId == null)
                return Ok(new { TeacherName = (string?)null });

            var teacher = await _context.Users.FirstOrDefaultAsync(u => u.Id == student.UserId);
            return Ok(new
            {
                TeacherId = teacher?.Id,
                TeacherName = teacher?.Username
            });
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
