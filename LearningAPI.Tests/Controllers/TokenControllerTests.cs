using FluentAssertions;
using LearningAPI.Controllers;
using LearningAPI.Tests.Helpers;
using LearningTrainerShared.Context;
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

public class TokenControllerTests : IDisposable
{
    private readonly ApiDbContext _context;
    private readonly TokenService _tokenService;
    private readonly Mock<ILogger<TokenController>> _loggerMock;
    private readonly TokenController _controller;
    private readonly int _testUserId = 1;

    public TokenControllerTests()
    {
        _context = TestDbContextFactory.CreateInMemoryContext();
        
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["Jwt:Key"]).Returns("SuperSecretKeyForTestingPurposesOnly123456");
        configMock.Setup(c => c["Jwt:Issuer"]).Returns("TestIssuer");
        configMock.Setup(c => c["Jwt:Audience"]).Returns("TestAudience");
        configMock.Setup(c => c["Jwt:RefreshTokenExpiryDays"]).Returns("7");
        configMock.Setup(c => c["Jwt:ExpiresHours"]).Returns("2");
        
        _tokenService = new TokenService(configMock.Object);
        _loggerMock = new Mock<ILogger<TokenController>>();
        
        _controller = new TokenController(_context, _tokenService, _loggerMock.Object);
    }

    private void SetupUserContext(int userId)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, "User")
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

    private async Task<User> CreateUserWithRefreshToken(string refreshToken, DateTime expiryTime, bool isRevoked = false)
    {
        var role = TestDataSeeder.CreateTeacherRole();
        if (!await _context.Roles.AnyAsync(r => r.Id == role.Id))
        {
            _context.Roles.Add(role);
        }
        
        var user = new User
        {
            Id = _testUserId,
            Login = "testuser",
            PasswordHash = "hash",
            Role = role,
            RefreshToken = refreshToken,
            RefreshTokenExpiryTime = expiryTime,
            IsRefreshTokenRevoked = isRevoked
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    #region RefreshToken Tests

    [Fact]
    public async Task RefreshToken_WithValidToken_ReturnsNewTokens()
    {
        // Arrange
        var refreshToken = "valid-refresh-token";
        await CreateUserWithRefreshToken(refreshToken, DateTime.UtcNow.AddDays(7));

        var request = new TokenController.RefreshTokenRequest { RefreshToken = refreshToken };

        // Act
        var result = await _controller.RefreshToken(request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().NotBeNull();
        
        // Verify old refresh token was replaced
        var user = await _context.Users.FindAsync(_testUserId);
        user!.RefreshToken.Should().NotBe(refreshToken);
    }

    [Fact]
    public async Task RefreshToken_WithEmptyToken_ReturnsBadRequest()
    {
        // Arrange
        var request = new TokenController.RefreshTokenRequest { RefreshToken = "" };

        // Act
        var result = await _controller.RefreshToken(request);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task RefreshToken_WithNullRequest_ReturnsBadRequest()
    {
        // Arrange
        var request = new TokenController.RefreshTokenRequest { RefreshToken = null! };

        // Act
        var result = await _controller.RefreshToken(request);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task RefreshToken_WithNonExistentToken_ReturnsUnauthorized()
    {
        // Arrange
        var request = new TokenController.RefreshTokenRequest { RefreshToken = "non-existent-token" };

        // Act
        var result = await _controller.RefreshToken(request);

        // Assert
        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task RefreshToken_WithExpiredToken_ReturnsUnauthorized()
    {
        // Arrange
        var refreshToken = "expired-refresh-token";
        await CreateUserWithRefreshToken(refreshToken, DateTime.UtcNow.AddDays(-1)); // Expired

        var request = new TokenController.RefreshTokenRequest { RefreshToken = refreshToken };

        // Act
        var result = await _controller.RefreshToken(request);

        // Assert
        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task RefreshToken_WithRevokedToken_ReturnsUnauthorized()
    {
        // Arrange
        var refreshToken = "revoked-refresh-token";
        await CreateUserWithRefreshToken(refreshToken, DateTime.UtcNow.AddDays(7), isRevoked: true);

        var request = new TokenController.RefreshTokenRequest { RefreshToken = refreshToken };

        // Act
        var result = await _controller.RefreshToken(request);

        // Assert
        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    #endregion

    #region RevokeToken Tests

    [Fact]
    public async Task RevokeToken_WithValidToken_RevokesSuccessfully()
    {
        // Arrange
        var refreshToken = "token-to-revoke";
        await CreateUserWithRefreshToken(refreshToken, DateTime.UtcNow.AddDays(7));
        SetupUserContext(_testUserId);

        var request = new TokenController.RevokeTokenRequest { RefreshToken = refreshToken };

        // Act
        var result = await _controller.RevokeToken(request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        
        var user = await _context.Users.FindAsync(_testUserId);
        user!.IsRefreshTokenRevoked.Should().BeTrue();
    }

    [Fact]
    public async Task RevokeToken_WithEmptyToken_ReturnsBadRequest()
    {
        // Arrange
        var role = TestDataSeeder.CreateTeacherRole();
        _context.Roles.Add(role);
        var user = new User { Id = _testUserId, Login = "test", PasswordHash = "hash", Role = role };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        
        SetupUserContext(_testUserId);
        var request = new TokenController.RevokeTokenRequest { RefreshToken = "" };

        // Act
        var result = await _controller.RevokeToken(request);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task RevokeToken_WithUserNotFound_ReturnsNotFound()
    {
        // Arrange
        SetupUserContext(999); // Non-existent user
        var request = new TokenController.RevokeTokenRequest { RefreshToken = "some-token" };

        // Act
        var result = await _controller.RevokeToken(request);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    #endregion
}

// Request/Response classes for the controller
public class RefreshTokenRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}

public class RefreshTokenResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public int ExpiresIn { get; set; }
    public string TokenType { get; set; } = string.Empty;
}

public class RevokeTokenRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}
