using FluentAssertions;
using LearningAPI.Controllers;
using LearningAPI.Tests.Helpers;
using LearningTrainerShared.Context;
using LearningTrainerShared.Models;
using LearningTrainerShared.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace LearningAPI.Tests.Controllers;

public class AuthControllerTests : IDisposable
{
    private readonly ApiDbContext _context;
    private readonly TokenService _tokenService;
    private readonly Mock<ILogger<AuthController>> _loggerMock;
    private readonly AuthController _controller;

    public AuthControllerTests()
    {
        _context = TestDbContextFactory.CreateInMemoryContext();
        
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["Jwt:Key"]).Returns("SuperSecretKeyForTestingPurposesOnly123456");
        configMock.Setup(c => c["Jwt:Issuer"]).Returns("TestIssuer");
        configMock.Setup(c => c["Jwt:Audience"]).Returns("TestAudience");
        configMock.Setup(c => c["Jwt:RefreshTokenExpiryDays"]).Returns("7");
        
        _tokenService = new TokenService(configMock.Object);
        _loggerMock = new Mock<ILogger<AuthController>>();
        
        _controller = new AuthController(_context, _tokenService, _loggerMock.Object);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsOkWithTokens()
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
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value;
        
        response.Should().NotBeNull();
        response!.GetType().GetProperty("AccessToken")?.GetValue(response).Should().NotBeNull();
        response!.GetType().GetProperty("RefreshToken")?.GetValue(response).Should().NotBeNull();
        response!.GetType().GetProperty("Username")?.GetValue(response).Should().Be("testuser");
    }

    [Fact]
    public async Task Login_WithInvalidPassword_ReturnsUnauthorized()
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
            Password = "wrongpassword"
        };

        // Act
        var result = await _controller.Login(request);

        // Assert
        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task Login_WithNonExistentUser_ReturnsUnauthorized()
    {
        // Arrange
        var request = new AuthController.LoginRequest
        {
            Username = "nonexistent",
            Password = "password123"
        };

        // Act
        var result = await _controller.Login(request);

        // Assert
        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task Register_WithNewUser_ReturnsCreated()
    {
        // Arrange - now registration without invite code creates "User" role
        var userRole = new Role { Id = 4, Name = "User" };
        _context.Roles.Add(userRole);
        await _context.SaveChangesAsync();

        var request = new RegisterRequest
        {
            Login = "newuser",
            Password = "password123",
            InviteCode = null
        };

        // Act
        var result = await _controller.Register(request);

        // Assert
        result.Should().BeOfType<CreatedAtActionResult>();

        var createdUser = await _context.Users.FirstOrDefaultAsync(u => u.Username == "newuser");
        createdUser.Should().NotBeNull();
    }

    [Fact]
    public async Task Register_WithExistingLogin_ReturnsBadRequest()
    {
        // Arrange
        var role = TestDataSeeder.CreateTeacherRole();
        _context.Roles.Add(role);
        
        var existingUser = TestDataSeeder.CreateTestUser("existinguser", "password123", role);
        _context.Users.Add(existingUser);
        await _context.SaveChangesAsync();

        var request = new RegisterRequest
        {
            Login = "existinguser",
            Password = "password123"
        };

        // Act
        var result = await _controller.Register(request);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }
}
