using LearningTrainerShared.Context;
using LearningTrainerShared.Services;
using LearningTrainerShared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BCrypt.Net;
using NanoidDotNet;

namespace LearningAPI.Controllers
{
    [Route("api/[controller]")] // /api/auth
    public class AuthController : BaseApiController
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
        private string GenerateInviteCode() => $"TR-{Nanoid.Generate(size: 6)}";
        
        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            try
            {
                // Ищем по Username или Email
                var user = await _context.Users
                                   .Include(u => u.Role)
                                   .FirstOrDefaultAsync(u => u.Username == request.Username || u.Email == request.Username);

                if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
                {
                    _logger.LogWarning("Failed login attempt for user {Username}", request.Username);
                    return Unauthorized(new { message = "Неверный логин или пароль" });
                }

                var accessToken = _tokenService.GenerateAccessToken(user);
                var refreshToken = _tokenService.GenerateRefreshToken();

                // Сохраняем refresh token в БД
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
                    Username = user.Username,
                    UserLogin = user.Username, // для обратной совместимости
                    Email = user.Email,
                    UserRole = user.Role?.Name ?? "User",
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
            var userId = GetUserId();

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

        [AllowAnonymous]
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            // Проверка уникальности Username
            if (await _context.Users.AnyAsync(u => u.Username == request.Username))
            {
                return BadRequest(new { message = "Это имя пользователя уже занято" });
            }

            // Проверка уникальности Email
            if (await _context.Users.AnyAsync(u => u.Email == request.Email))
            {
                return BadRequest(new { message = "Этот Email уже зарегистрирован" });
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
                    .FirstOrDefaultAsync(r => r.Name == "User");
            }

            if (roleToAssign == null)
            {
                return StatusCode(500, new { message = "Ошибка: Роль не найдена в БД." });
            }

            var newUser = new User
            {
                Username = request.Username,
                Email = request.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                RoleId = roleToAssign.Id,
                UserId = teacherId,
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(Login), new { username = newUser.Username }, new { message = "Аккаунт успешно создан" });
        }

        [Authorize]
        [HttpPost("upgrade-to-teacher")]
        public async Task<IActionResult> UpgradeToTeacher()
        {
            var userId = GetUserId();
            var user = await _context.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null) return NotFound("User not found.");

            // User, Admin и IndependentUser могут стать учителем
            if (user.Role?.Name != "Admin" && user.Role?.Name != "User" && user.Role?.Name != "IndependentUser")
            {
                return StatusCode(403, new { message = "Только пользователь или администратор может стать учителем." });
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
                Message = "Вы стали учителем. Вот приглашение.",
                InviteCode = user.InviteCode,
                AccessToken = newAccessToken,
                UserRole = "Teacher"
            });
        }
    }
}

