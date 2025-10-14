using EnglishLearningTrainer.Context;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LearningAPI.Controllers
{
    [Route("api/[controller]")] // /api/auth
    public class AuthController : ControllerBase
    {
        private readonly ApiDbContext _context;
        private HttpClient _httpClient;

        public AuthController(ApiDbContext context)
        {
            _context = context;
        }

        public class LoginRequest
        {
            public string Username { get; set; }
            public string Password { get; set; }
        }

        [HttpPost("login")] // /api/auth/login
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var user = await _context.Users
                               .Include(u => u.Role)
                               .FirstOrDefaultAsync(u => u.Login == request.Username && u.PasswordHash == request.Password);

            if (user == null)
            {
                return Unauthorized(new { message = "Неверный логин или пароль" });
            }

            return Ok(user);
        }



    }
}
