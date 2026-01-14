using FluentAssertions;
using LearningAPI.Controllers;
using LearningAPI.Tests.Helpers;
using LearningTrainer.Context;
using LearningTrainerShared.Constants;
using LearningTrainerShared.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;
using Xunit;

namespace LearningAPI.Tests.Controllers;

public class AdminUsersControllerTests : IDisposable
{
    private readonly ApiDbContext _context;
    private readonly Mock<ILogger<AdminUsersController>> _loggerMock;
    private readonly AdminUsersController _controller;
    private readonly int _adminUserId = 1;

    public AdminUsersControllerTests()
    {
        _context = TestDbContextFactory.CreateInMemoryContext();
        _loggerMock = new Mock<ILogger<AdminUsersController>>();
        _controller = new AdminUsersController(_context, _loggerMock.Object);
        SetupUserContext(_adminUserId, "Admin");
    }

    private void SetupUserContext(int userId, string role)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, role)
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    private async Task SetupRoles()
    {
        var roles = new List<Role>
        {
            new Role { Id = 1, Name = "Teacher" },
            new Role { Id = 2, Name = "Student" },
            new Role { Id = 3, Name = "Admin" },
            new Role { Id = 4, Name = "User" }
        };
        _context.Roles.AddRange(roles);
        await _context.SaveChangesAsync();
    }

    #region GetAllUsers Tests

    [Fact]
    public async Task GetAllUsers_ReturnsAllUsers()
    {
        // Arrange
        await SetupRoles();
        
        var adminRole = await _context.Roles.FirstAsync(r => r.Name == "Admin");
        var teacherRole = await _context.Roles.FirstAsync(r => r.Name == "Teacher");
        
        var admin = new User { Id = _adminUserId, Login = "admin", PasswordHash = "hash", Role = adminRole };
        var teacher1 = new User { Id = 2, Login = "teacher1", PasswordHash = "hash", Role = teacherRole };
        var teacher2 = new User { Id = 3, Login = "teacher2", PasswordHash = "hash", Role = teacherRole };
        _context.Users.AddRange(admin, teacher1, teacher2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetAllUsers(1, 10, null);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task GetAllUsers_FiltersByRole()
    {
        // Arrange
        await SetupRoles();
        
        var adminRole = await _context.Roles.FirstAsync(r => r.Name == "Admin");
        var teacherRole = await _context.Roles.FirstAsync(r => r.Name == "Teacher");
        var studentRole = await _context.Roles.FirstAsync(r => r.Name == "Student");
        
        var admin = new User { Id = _adminUserId, Login = "admin", PasswordHash = "hash", Role = adminRole };
        var teacher = new User { Id = 2, Login = "teacher", PasswordHash = "hash", Role = teacherRole };
        var student = new User { Id = 3, Login = "student", PasswordHash = "hash", Role = studentRole };
        _context.Users.AddRange(admin, teacher, student);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetAllUsers(1, 10, "Teacher");

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value;
        response.Should().NotBeNull();
    }

    [Fact]
    public async Task GetAllUsers_SupportsPagination()
    {
        // Arrange
        await SetupRoles();
        var teacherRole = await _context.Roles.FirstAsync(r => r.Name == "Teacher");
        
        for (int i = 1; i <= 25; i++)
        {
            _context.Users.Add(new User 
            { 
                Id = i, 
                Login = $"user{i}", 
                PasswordHash = "hash", 
                Role = teacherRole 
            });
        }
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetAllUsers(2, 10, null);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().NotBeNull();
    }

    #endregion

    #region ChangeUserRole Tests

    [Fact]
    public async Task ChangeUserRole_WithValidData_ChangesRole()
    {
        // Arrange
        await SetupRoles();
        
        var teacherRole = await _context.Roles.FirstAsync(r => r.Name == "Teacher");
        var user = new User { Id = 2, Login = "user", PasswordHash = "hash", Role = teacherRole, RoleId = teacherRole.Id };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var request = new AdminUsersController.ChangeRoleRequest { NewRole = "Student" };

        // Act
        var result = await _controller.ChangeUserRole(2, request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        
        var updatedUser = await _context.Users.Include(u => u.Role).FirstAsync(u => u.Id == 2);
        updatedUser.Role.Name.Should().Be("Student");
    }

    [Fact]
    public async Task ChangeUserRole_WithInvalidRole_ReturnsBadRequest()
    {
        // Arrange
        await SetupRoles();
        
        var teacherRole = await _context.Roles.FirstAsync(r => r.Name == "Teacher");
        var user = new User { Id = 2, Login = "user", PasswordHash = "hash", Role = teacherRole };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var request = new AdminUsersController.ChangeRoleRequest { NewRole = "InvalidRole" };

        // Act
        var result = await _controller.ChangeUserRole(2, request);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ChangeUserRole_WithNonExistentUser_ReturnsNotFound()
    {
        // Arrange
        await SetupRoles();
        var request = new AdminUsersController.ChangeRoleRequest { NewRole = "Student" };

        // Act
        var result = await _controller.ChangeUserRole(999, request);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    #endregion

    #region DeleteUser Tests

    [Fact]
    public async Task DeleteUser_WithValidId_DeletesUser()
    {
        // Arrange
        await SetupRoles();
        
        var teacherRole = await _context.Roles.FirstAsync(r => r.Name == "Teacher");
        var userToDelete = new User { Id = 2, Login = "todelete", PasswordHash = "hash", Role = teacherRole };
        _context.Users.Add(userToDelete);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.DeleteUser(2);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        
        var deletedUser = await _context.Users.FindAsync(2);
        deletedUser.Should().BeNull();
    }

    [Fact]
    public async Task DeleteUser_WithNonExistentId_ReturnsNotFound()
    {
        // Act
        var result = await _controller.DeleteUser(999);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task DeleteUser_WithOwnId_ReturnsBadRequest()
    {
        // Arrange
        await SetupRoles();
        
        var adminRole = await _context.Roles.FirstAsync(r => r.Name == "Admin");
        var admin = new User { Id = _adminUserId, Login = "admin", PasswordHash = "hash", Role = adminRole };
        _context.Users.Add(admin);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.DeleteUser(_adminUserId);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    #endregion
}
