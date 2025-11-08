using LearningTrainer.Context;
using LearningTrainer.Services;
using LearningTrainerShared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using BCrypt.Net;

namespace LearningAPI.Controllers
{
    [Route("api/[controller]")] // /api/auth
    public class AuthController : ControllerBase
    {
        private readonly ApiDbContext _context;
        private readonly TokenService _tokenService;
        private HttpClient _httpClient;

        public AuthController(ApiDbContext context, TokenService tokenService)
        {
            _context = context;
            _tokenService = tokenService;
        }

        public class LoginRequest
        {
            public string Username { get; set; }
            public string Password { get; set; }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var user = await _context.Users
                               .Include(u => u.Role)
                               .FirstOrDefaultAsync(u => u.Login == request.Username);

            if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                return Unauthorized(new { message = "Неверный логин или пароль" });
            }

            var accessToken = _tokenService.GenerateAccessToken(user);

            return Ok(new
            {
                AccessToken = accessToken,
                UserLogin = user.Login,
                UserRole = user.Role.Name
            });
        }

        [Authorize]
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdString, out var userId))
            {
                return Unauthorized();
            }

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return NotFound("User not found.");
            }

            if (!BCrypt.Net.BCrypt.Verify(request.OldPassword, user.PasswordHash))
            {
                return BadRequest(new { message = "Неверный старый пароль" });
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            await _context.SaveChangesAsync();            

            return Ok(new { message = "Пароль успешно обновлен" });
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            if (await _context.Users.AnyAsync(u => u.Login == request.Login))
            {
                return BadRequest(new { message = "Этот логин уже занят" });
            }

            var studentRole = await _context.Roles
                .FirstOrDefaultAsync(r => r.Name == "Student");

            if (studentRole == null)
            {
                return StatusCode(500, new { message = "Критическая ошибка: Роль 'Student' не найдена в базе данных." });
            }

            var newUser = new User
            {
                Login = request.Login,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                RoleId = studentRole.Id,
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            // 201 Created
            return CreatedAtAction(nameof(Login), new { username = newUser.Login }, new { message = "Аккаунт успешно создан" });
        }
    }
}
