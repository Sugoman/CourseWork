using LearningTrainer.Context;
using LearningTrainerShared.Services;
using LearningTrainerShared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using BCrypt.Net;
using NanoidDotNet;

namespace LearningAPI.Controllers
{
    [Route("api/[controller]")] // /api/auth
    public class AuthController : ControllerBase
    {
        private readonly ApiDbContext _context;
        private readonly TokenService _tokenService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(ApiDbContext context, TokenService tokenService, ILogger<AuthController> logger)
        {
            _context = context;
            _tokenService = tokenService;
            _logger = logger;
        }

        public class LoginRequest
        {
            public string Username { get; set; }
            public string Password { get; set; }
        }
        private int GetUserId() => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        private string GenerateInviteCode() => $"TR-{Nanoid.Generate(size: 6)}";
        
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            try
            {
                var user = await _context.Users
                                   .Include(u => u.Role)
                                   .FirstOrDefaultAsync(u => u.Login == request.Username);

                if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
                {
                    _logger.LogWarning("Failed login attempt for user {Username}", request.Username);
                    return Unauthorized(new { message = "Неверный логин или пароль" });
                }

                var accessToken = _tokenService.GenerateAccessToken(user);
                var refreshToken = _tokenService.GenerateRefreshToken();

                // Сохранить refresh token в БД
                user.RefreshToken = refreshToken;
                user.RefreshTokenExpiryTime = _tokenService.GetRefreshTokenExpiryTime();
                user.IsRefreshTokenRevoked = false;
                await _context.SaveChangesAsync();

                _logger.LogInformation("User {UserId} logged in successfully", user.Id);

                return Ok(new
                {
                    AccessToken = accessToken,
                    RefreshToken = refreshToken,
                    TokenType = "Bearer",
                    ExpiresIn = 7200, // 2 hours in seconds
                    UserLogin = user.Login,
                    UserRole = user.Role.Name,
                    UserId = user.Id,         
                    InviteCode = user.InviteCode
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login");
                return StatusCode(500, new { message = "An error occurred during login" });
            }
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

            Role roleToAssign;
            int? teacherId = null;

            if (!string.IsNullOrWhiteSpace(request.InviteCode))
            {
                var teacher = await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.InviteCode == request.InviteCode);

                if (teacher != null)
                {
                    roleToAssign = await _context.Roles.AsNoTracking()
                        .FirstOrDefaultAsync(r => r.Name == "Student");
                    teacherId = teacher.Id;

                }
                else
                {
                    return BadRequest(new { message = "Неверный код приглашения" });
                }
            }
            else
            {
                roleToAssign = await _context.Roles.AsNoTracking()
                    .FirstOrDefaultAsync(r => r.Name == "Admin");
            }

            if (roleToAssign == null)
            {
                return StatusCode(500, new { message = "Ошибка: Роль не найдена в БД." });
            }

            var newUser = new User
            {
                Login = request.Login,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                RoleId = roleToAssign.Id,
                UserId = teacherId,
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(Login), new { username = newUser.Login }, new { message = "Аккаунт успешно создан" });
        }

        [Authorize]
        [HttpPost("upgrade-to-teacher")]
        public async Task<IActionResult> UpgradeToTeacher()
        {
            var userId = GetUserId();
            var user = await _context.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null) return NotFound("User not found.");

            if (user.Role.Name != "Admin" && user.Role.Name != "IndependentUser")
            {
                return StatusCode(403, new { message = "Только администратор может стать учителем." });
            }

            var teacherRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Teacher");
            if (teacherRole == null)
            {
                return StatusCode(500, new { message = "Критическая ошибка: Роль 'Teacher' не найдена в базе данных." });
            }

            user.RoleId = teacherRole.Id;
            user.InviteCode = GenerateInviteCode();

            await _context.SaveChangesAsync();
            var newAccessToken = _tokenService.GenerateAccessToken(user);

            return Ok(new
            {
                Message = "Вы стали учителем. Код сгенерирован.",
                InviteCode = user.InviteCode,
                AccessToken = newAccessToken,
                UserRole = "Teacher"
            });
        }
    }
}
