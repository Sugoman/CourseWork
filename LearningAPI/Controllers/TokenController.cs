using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using LearningTrainer.Context;
using LearningTrainerShared.Services;
using System.Security.Claims;

namespace LearningAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TokenController : ControllerBase
    {
        private readonly ApiDbContext _context;
        private readonly TokenService _tokenService;
        private readonly ILogger<TokenController> _logger;

        public TokenController(ApiDbContext context, TokenService tokenService, ILogger<TokenController> logger)
        {
            _context = context;
            _tokenService = tokenService;
            _logger = logger;
        }

        /// <summary>
        /// �������� access token ��������� refresh token
        /// </summary>
        [HttpPost("refresh")]
        [AllowAnonymous]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request?.RefreshToken))
                {
                    _logger.LogWarning("Refresh token is missing in request");
                    return BadRequest(new { message = "Refresh token is required" });
                }

                // ����� ������������ � ���� refresh token
                var user = await _context.Users
                    .Include(u => u.Role)
                    .FirstOrDefaultAsync(u => u.RefreshToken == request.RefreshToken);

                if (user == null)
                {
                    _logger.LogWarning("User with refresh token not found");
                    return Unauthorized(new { message = "Invalid refresh token" });
                }

                // ���������, �� ���� �� refresh token
                if (user.RefreshTokenExpiryTime < DateTime.UtcNow)
                {
                    _logger.LogWarning("Refresh token expired for user {UserId}", user.Id);
                    return Unauthorized(new { message = "Refresh token has expired" });
                }

                // ���������, �� ��� �� ������� refresh token
                if (user.IsRefreshTokenRevoked)
                {
                    _logger.LogWarning("Refresh token is revoked for user {UserId}", user.Id);
                    return Unauthorized(new { message = "Refresh token has been revoked" });
                }

                // ������������� ����� access token
                var newAccessToken = _tokenService.GenerateAccessToken(user);
                var newRefreshToken = _tokenService.GenerateRefreshToken();

                // �������� refresh token � ��
                user.RefreshToken = newRefreshToken;
                user.RefreshTokenExpiryTime = _tokenService.GetRefreshTokenExpiryTime();
                await _context.SaveChangesAsync();

                _logger.LogInformation("Token refreshed successfully for user {UserId}", user.Id);

                return Ok(new RefreshTokenResponse
                {
                    AccessToken = newAccessToken,
                    RefreshToken = newRefreshToken,
                    ExpiresIn = 7200, // 2 hours in seconds
                    TokenType = "Bearer"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing token");
                return StatusCode(500, new { message = "An error occurred while refreshing the token" });
            }
        }

        /// <summary>
        /// �������� refresh token (����� �� �������)
        /// </summary>
        [HttpPost("revoke")]
        [Authorize]
        public async Task<IActionResult> RevokeToken([FromBody] RevokeTokenRequest request)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!int.TryParse(userIdClaim, out var userId))
                {
                    return Unauthorized(new { message = "Invalid user" });
                }

                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    return NotFound();
                }

                // �������� ������� refresh token
                if (!string.IsNullOrEmpty(request?.RefreshToken))
                {
                    user.IsRefreshTokenRevoked = true;
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Refresh token revoked for user {UserId}", userId);

                    return Ok(new { message = "Token revoked successfully" });
                }

                return BadRequest(new { message = "Refresh token is required" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking token for user {UserId}", User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
                return StatusCode(500, new { message = "An error occurred while revoking the token" });
            }
        }

        /// <summary>
        /// �������� ��� refresh tokens ������������ (����� �� ���� ���������)
        /// </summary>
        [HttpPost("revoke-all")]
        [Authorize]
        public async Task<IActionResult> RevokeAllTokens()
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!int.TryParse(userIdClaim, out var userId))
                {
                    return Unauthorized(new { message = "Invalid user" });
                }

                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    return NotFound();
                }

                // �������� ��� refresh tokens ������������
                user.RefreshToken = null;
                user.IsRefreshTokenRevoked = true;
                user.RefreshTokenExpiryTime = null;
                await _context.SaveChangesAsync();

                _logger.LogInformation("All refresh tokens revoked for user {UserId}", userId);

                return Ok(new { message = "All tokens revoked successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking all tokens for user {UserId}", User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
                return StatusCode(500, new { message = "An error occurred while revoking tokens" });
            }
        }

        public class RefreshTokenRequest
        {
            public string RefreshToken { get; set; }
        }

        public class RefreshTokenResponse
        {
            public string AccessToken { get; set; }
            public string RefreshToken { get; set; }
            public int ExpiresIn { get; set; }
            public string TokenType { get; set; }
        }

        public class RevokeTokenRequest
        {
            public string RefreshToken { get; set; }
        }
    }
}
