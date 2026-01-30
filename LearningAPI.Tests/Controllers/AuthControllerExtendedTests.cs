using FluentAssertions;
using LearningAPI.Controllers;
using LearningAPI.Tests.Helpers;
using LearningTrainer.Context;
using LearningTrainerShared.Models;
using LearningTrainerShared.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;
using Xunit;

namespace LearningAPI.Tests.Controllers;

public class AuthControllerExtendedTests : IDisposable
{
    private readonly ApiDbContext _context;
    private readonly TokenService _tokenService;
    private readonly Mock<ILogger<AuthController>> _loggerMock;
    private readonly AuthController _controller;

    public AuthControllerExtendedTests()
    {
        _context = TestDbContextFactory.CreateInMemoryContext();
        
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["Jwt:Key"]).Returns("SuperSecretKeyForTestingPurposesOnly123456");
        configMock.Setup(c => c["Jwt:Issuer"]).Returns("TestIssuer");
        configMock.Setup(c => c["Jwt:Audience"]).Returns("TestAudience");
        configMock.Setup(c => c["Jwt:RefreshTokenExpiryDays"]).Returns("7");
        configMock.Setup(c => c["Jwt:ExpiresHours"]).Returns("2");
        
        _tokenService = new TokenService(configMock.Object);
        _loggerMock = new Mock<ILogger<AuthController>>();
        
        _controller = new AuthController(_context, _tokenService, _loggerMock.Object);
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

    #region Login Extended Tests

    [Fact]
    public async Task Login_WithValidCredentials_SavesRefreshToken()
    {
        // Arrange
        var role = TestDataSeeder.CreateTeacherRole();
        _context.Roles.Add(role);
        
        var user = TestDataSeeder.CreateTestUser("testuser", "password123", role);
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var request = new AuthController.LoginRequest
        {
            Username = "testuser",
            Password = "password123"
        };

        // Act
        var result = await _controller.Login(request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        
        var updatedUser = await _context.Users.FirstAsync(u => u.Username == "testuser");
        updatedUser.RefreshToken.Should().NotBeNullOrEmpty();
        updatedUser.RefreshTokenExpiryTime.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public async Task Login_WithEmptyUsername_ReturnsUnauthorized()
    {
        // Arrange
        var request = new AuthController.LoginRequest
        {
            Username = "",
            Password = "password123"
        };

        // Act
        var result = await _controller.Login(request);

        // Assert
        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task Login_WithEmptyPassword_ReturnsUnauthorized()
    {
        // Arrange
        var role = TestDataSeeder.CreateTeacherRole();
        _context.Roles.Add(role);
        
        var user = TestDataSeeder.CreateTestUser("testuser", "password123", role);
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var request = new AuthController.LoginRequest
        {
            Username = "testuser",
            Password = ""
        };

        // Act
        var result = await _controller.Login(request);

        // Assert
        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    #endregion

    #region Register Extended Tests

    [Fact]
    public async Task Register_WithTeacherInviteCode_AssignsStudentRole()
    {
        // Arrange
        var teacherRole = TestDataSeeder.CreateTeacherRole();
        var studentRole = TestDataSeeder.CreateStudentRole();
        _context.Roles.AddRange(teacherRole, studentRole);
        
        var teacher = new User
        {
            Id = 1,
            Username = "teacher",
            Email = "teacher@test.com",
            PasswordHash = "hash",
            Role = teacherRole,
            InviteCode = "TR-INVITE1"
        };
        _context.Users.Add(teacher);
        await _context.SaveChangesAsync();

        var request = new RegisterRequest
        {
            Username = "newstudent",
            Email = "newstudent@test.com",
            Password = "password123",
            InviteCode = "TR-INVITE1"
        };

        // Act
        var result = await _controller.Register(request);

        // Assert
        result.Should().BeOfType<CreatedAtActionResult>();
        
        var newUser = await _context.Users.FirstAsync(u => u.Username == "newstudent");
        newUser.UserId.Should().Be(1); // Linked to teacher
    }

    [Fact]
    public async Task Register_WithInvalidInviteCode_ReturnsBadRequest()
    {
        // Arrange
        var userRole = TestDataSeeder.CreateUserRole();
        _context.Roles.Add(userRole);
        await _context.SaveChangesAsync();

        var request = new RegisterRequest
        {
            Username = "newuser",
            Email = "newuser@test.com",
            Password = "password123",
            InviteCode = "INVALID-CODE"
        };

        // Act
        var result = await _controller.Register(request);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Register_HashesPassword()
    {
        // Arrange
        var userRole = TestDataSeeder.CreateUserRole();
        _context.Roles.Add(userRole);
        await _context.SaveChangesAsync();

        var request = new RegisterRequest
        {
            Username = "newuser",
            Email = "newuser@test.com",
            Password = "password123"
        };

        // Act
        var result = await _controller.Register(request);

        // Assert
        result.Should().BeOfType<CreatedAtActionResult>();
        
        var newUser = await _context.Users.FirstAsync(u => u.Username == "newuser");
        newUser.PasswordHash.Should().NotBe("password123");
        newUser.PasswordHash.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region ChangePassword Tests

    [Fact]
    public async Task ChangePassword_WithValidData_ChangesPassword()
    {
        // Arrange
        var role = TestDataSeeder.CreateTeacherRole();
        _context.Roles.Add(role);
        
        var user = TestDataSeeder.CreateTestUser("testuser", "oldpassword", role);
        user.Id = 1;
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        SetupUserContext(1, "Teacher");

        var request = new ChangePasswordRequest
        {
            OldPassword = "oldpassword",
            NewPassword = "newpassword123"
        };

        // Act
        var result = await _controller.ChangePassword(request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task ChangePassword_WithWrongCurrentPassword_ReturnsBadRequest()
    {
        // Arrange
        var role = TestDataSeeder.CreateTeacherRole();
        _context.Roles.Add(role);
        
        var user = TestDataSeeder.CreateTestUser("testuser", "correctpassword", role);
        user.Id = 1;
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        SetupUserContext(1, "Teacher");

        var request = new ChangePasswordRequest
        {
            OldPassword = "wrongpassword",
            NewPassword = "newpassword123"
        };

        // Act
        var result = await _controller.ChangePassword(request);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    #endregion

    #region UpgradeToTeacher Tests

    [Fact]
    public async Task UpgradeToTeacher_WithUserRole_UpgradesToTeacher()
    {
        // Arrange
        var userRole = TestDataSeeder.CreateUserRole();
        var teacherRole = TestDataSeeder.CreateTeacherRole();
        _context.Roles.AddRange(userRole, teacherRole);
        
        var user = new User
        {
            Id = 1,
            Login = "testuser",
            PasswordHash = "hash",
            Role = userRole,
            RoleId = userRole.Id
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        SetupUserContext(1, "User");

        // Act
        var result = await _controller.UpgradeToTeacher();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        
        var updatedUser = await _context.Users.Include(u => u.Role).FirstAsync(u => u.Id == 1);
        updatedUser.Role.Name.Should().Be("Teacher");
        updatedUser.InviteCode.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task UpgradeToTeacher_WithStudentRole_ReturnsForbidden()
    {
        // Arrange
        var studentRole = TestDataSeeder.CreateStudentRole();
        var teacherRole = TestDataSeeder.CreateTeacherRole();
        _context.Roles.AddRange(studentRole, teacherRole);
        
        var user = new User
        {
            Id = 1,
            Login = "student",
            PasswordHash = "hash",
            Role = studentRole,
            RoleId = studentRole.Id
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        SetupUserContext(1, "Student");

        // Act
        var result = await _controller.UpgradeToTeacher();

        // Assert
        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(403);
    }

    #endregion
}
