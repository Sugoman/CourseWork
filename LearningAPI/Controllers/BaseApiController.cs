using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace LearningAPI.Controllers
{
    [ApiController]
    [Authorize]
    public abstract class BaseApiController : ControllerBase
    {
        protected int GetUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (claim == null || string.IsNullOrEmpty(claim.Value))
            {
                throw new UnauthorizedAccessException("User ID not found in claims");
            }

            if (!int.TryParse(claim.Value, out var userId))
            {
                throw new InvalidOperationException("User ID is not a valid integer");
            }

            return userId;
        }

        protected string GetUserRole()
        {
            return User.FindFirst(ClaimTypes.Role)?.Value ?? "User";
        }

        protected string GetUserLogin()
        {
            return User.FindFirst(ClaimTypes.Name)?.Value ?? "Unknown";
        }
    }
}
