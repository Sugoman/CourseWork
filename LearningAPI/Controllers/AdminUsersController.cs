using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using LearningTrainer.Context;
using LearningTrainerShared.Models;
using LearningTrainerShared.Constants;
using System.Security.Claims;

namespace LearningAPI.Controllers
{
    [ApiController]
    [Route("api/admin/users")]
    [Authorize(Roles = UserRoles.Admin)]
    public class AdminUsersController : BaseApiController
    {
        private readonly ApiDbContext _context;
        private readonly ILogger<AdminUsersController> _logger;

        public AdminUsersController(ApiDbContext context, ILogger<AdminUsersController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// �������� ���� ������������� (������ ��� �������)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAllUsers(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string role = null)
        {
            try
            {
                var query = _context.Users.Include(u => u.Role).AsQueryable();

                // ������ �� ���� ���� �������
                if (!string.IsNullOrEmpty(role))
                {
                    query = query.Where(u => u.Role.Name == role);
                }

                var total = await query.CountAsync();
                var users = await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(u => new
                    {
                        u.Id,
                        u.Username,
                        u.Email,
                        role = u.Role.Name,
                        u.CreatedAt,
                        teacherId = u.UserId
                    })
                    .ToListAsync();

                _logger.LogInformation("Admin retrieved all users list");

                return Ok(new
                {
                    data = users,
                    pagination = new
                    {
                        page,
                        pageSize,
                        total,
                        pageCount = (int)Math.Ceiling(total / (double)pageSize)
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving users list");
                return StatusCode(500, new { message = "Error retrieving users" });
            }
        }

        /// <summary>
        /// �������� ���� ������������
        /// </summary>
        [HttpPut("{userId}/role")]
        public async Task<IActionResult> ChangeUserRole(int userId, [FromBody] ChangeRoleRequest request)
        {
            try
            {
                if (!UserRoles.AllRoles.Contains(request.NewRole))
                {
                    return BadRequest(new { message = "Invalid role" });
                }

                var user = await _context.Users
                    .Include(u => u.Role)
                    .FirstOrDefaultAsync(u => u.Id == userId);

                if (user == null)
                {
                    return NotFound();
                }

                var oldRole = user.Role.Name;
                var newRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == request.NewRole);

                if (newRole == null)
                {
                    return BadRequest(new { message = "Role not found" });
                }

                user.RoleId = newRole.Id;
                await _context.SaveChangesAsync();

                _logger.LogInformation("User {UserId} role changed from {OldRole} to {NewRole}", userId, oldRole, request.NewRole);

                return Ok(new
                {
                    message = "User role changed successfully",
                    userId = user.Id,
                    newRole = newRole.Name
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing user role");
                return StatusCode(500, new { message = "Error changing user role" });
            }
        }

        /// <summary>
        /// ������� ������������ (������ ��� �������)
        /// </summary>
        [HttpDelete("{userId}")]
        public async Task<IActionResult> DeleteUser(int userId)
        {
            try
            {
                var currentUserId = GetUserId();
                
                // ����� �� ����� ������� ����
                if (userId == currentUserId)
                {
                    return BadRequest(new { message = "Cannot delete yourself" });
                }

                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    return NotFound();
                }

                _context.Users.Remove(user);
                await _context.SaveChangesAsync();

                _logger.LogInformation("User {UserId} deleted by admin {AdminId}", userId, currentUserId);

                return Ok(new { message = "User deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user {UserId}", userId);
                return StatusCode(500, new { message = "Error deleting user" });
            }
        }

        /// <summary>
        /// �������� ���������� �� �������������
        /// </summary>
        [HttpGet("statistics")]
        public async Task<IActionResult> GetUserStatistics()
        {
            try
            {
                var totalUsers = await _context.Users.CountAsync();
                var adminCount = await _context.Users
                    .Where(u => u.Role.Name == UserRoles.Admin)
                    .CountAsync();
                var teacherCount = await _context.Users
                    .Where(u => u.Role.Name == UserRoles.Teacher)
                    .CountAsync();
                var studentCount = await _context.Users
                    .Where(u => u.Role.Name == UserRoles.Student)
                    .CountAsync();

                _logger.LogInformation("Admin requested user statistics");

                return Ok(new
                {
                    totalUsers,
                    byRole = new
                    {
                        admins = adminCount,
                        teachers = teacherCount,
                        students = studentCount
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user statistics");
                return StatusCode(500, new { message = "Error retrieving statistics" });
            }
        }

        public class ChangeRoleRequest
        {
            public string NewRole { get; set; }
        }
    }
}
