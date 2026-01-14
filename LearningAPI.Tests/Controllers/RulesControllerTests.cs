using FluentAssertions;
using LearningAPI.Controllers;
using LearningAPI.Tests.Helpers;
using LearningTrainer.Context;
using LearningTrainerShared.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Xunit;

namespace LearningAPI.Tests.Controllers;

public class RulesControllerTests : IDisposable
{
    private readonly ApiDbContext _context;
    private readonly RulesController _controller;
    private readonly int _testUserId = 1;

    public RulesControllerTests()
    {
        _context = TestDbContextFactory.CreateInMemoryContext();
        _controller = new RulesController(_context);
        SetupUserContext(_testUserId, "Teacher");
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

    #region GetRules Tests

    [Fact]
    public async Task GetRules_ReturnsUserRules()
    {
        // Arrange
        var role = TestDataSeeder.CreateTeacherRole();
        _context.Roles.Add(role);
        
        var user = new User { Id = _testUserId, Login = "testuser", PasswordHash = "hash", Role = role };
        _context.Users.Add(user);
        
        var rule = TestDataSeeder.CreateTestRule(_testUserId, "Test Rule");
        _context.Rules.Add(rule);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetRules();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task GetRules_ReturnsEmptyList_WhenNoRules()
    {
        // Arrange
        var role = TestDataSeeder.CreateTeacherRole();
        _context.Roles.Add(role);
        var user = new User { Id = _testUserId, Login = "testuser", PasswordHash = "hash", Role = role };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetRules();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task GetRules_ReturnsUnauthorized_WhenNoUserIdClaim()
    {
        // Arrange
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal() }
        };

        // Act
        var result = await _controller.GetRules();

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
    }

    #endregion

    #region GetAvailableRules Tests

    [Fact]
    public async Task GetAvailableRules_ReturnsOwnAndSharedRules()
    {
        // Arrange
        var role = TestDataSeeder.CreateTeacherRole();
        _context.Roles.Add(role);
        
        var user = new User { Id = _testUserId, Login = "testuser", PasswordHash = "hash", Role = role };
        var otherUser = new User { Id = 2, Login = "otheruser", PasswordHash = "hash", Role = role };
        _context.Users.AddRange(user, otherUser);
        
        var ownRule = new Rule { Id = 1, Title = "Own Rule", MarkdownContent = "Content", Description = "Test", Category = "Grammar", UserId = _testUserId };
        var sharedRule = new Rule { Id = 2, Title = "Shared Rule", MarkdownContent = "Content", Description = "Test", Category = "Grammar", UserId = 2 };
        _context.Rules.AddRange(ownRule, sharedRule);
        
        var sharing = new RuleSharing { RuleId = 2, StudentId = _testUserId, SharedAt = DateTime.UtcNow };
        _context.RuleSharings.Add(sharing);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetAvailableRules();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().NotBeNull();
    }

    #endregion

    #region AddRule Tests

    [Fact]
    public async Task AddRule_WithValidData_ReturnsCreatedRule()
    {
        // Arrange
        var ruleDto = new RuleCreateDto
        {
            Title = "New Rule",
            MarkdownContent = "# New Rule Content",
            Description = "Test description",
            Category = "Grammar",
            DifficultyLevel = 2,
            CreatedAt = DateTime.UtcNow
        };

        // Act
        var result = await _controller.AddRule(ruleDto);

        // Assert
        result.Should().BeOfType<CreatedAtActionResult>();
        
        var createdRule = await _context.Rules.FirstOrDefaultAsync(r => r.Title == "New Rule");
        createdRule.Should().NotBeNull();
        createdRule!.UserId.Should().Be(_testUserId);
    }

    [Fact]
    public async Task AddRule_WithNullData_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.AddRule(null!);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task AddRule_WithEmptyTitle_ReturnsBadRequest()
    {
        // Arrange
        var ruleDto = new RuleCreateDto
        {
            Title = "",
            MarkdownContent = "Content"
        };

        // Act
        var result = await _controller.AddRule(ruleDto);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task AddRule_WithWhitespaceTitle_ReturnsBadRequest()
    {
        // Arrange
        var ruleDto = new RuleCreateDto
        {
            Title = "   ",
            MarkdownContent = "Content"
        };

        // Act
        var result = await _controller.AddRule(ruleDto);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    #endregion

    #region DeleteRule Tests

    [Fact]
    public async Task DeleteRule_WithValidId_ReturnsNoContent()
    {
        // Arrange
        var rule = new Rule
        {
            Id = 1,
            Title = "To Delete",
            MarkdownContent = "Content",
            Description = "Test",
            Category = "Grammar",
            UserId = _testUserId
        };
        _context.Rules.Add(rule);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.DeleteRule(1);

        // Assert
        result.Should().BeOfType<NoContentResult>();
        
        var deletedRule = await _context.Rules.FindAsync(1);
        deletedRule.Should().BeNull();
    }

    [Fact]
    public async Task DeleteRule_WithNonExistentId_ReturnsNotFound()
    {
        // Act
        var result = await _controller.DeleteRule(999);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task DeleteRule_WithOtherUsersRule_ReturnsForbid()
    {
        // Arrange
        var rule = new Rule
        {
            Id = 1,
            Title = "Other's Rule",
            MarkdownContent = "Content",
            Description = "Test",
            Category = "Grammar",
            UserId = 999 // Different user
        };
        _context.Rules.Add(rule);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.DeleteRule(1);

        // Assert
        result.Should().BeOfType<ForbidResult>();
    }

    #endregion

    #region UpdateRule Tests

    [Fact]
    public async Task UpdateRule_WithValidData_ReturnsNoContent()
    {
        // Arrange
        var existingRule = new Rule
        {
            Id = 1,
            Title = "Original Title",
            MarkdownContent = "Original Content",
            Description = "Test",
            Category = "Grammar",
            UserId = _testUserId,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };
        _context.Rules.Add(existingRule);
        await _context.SaveChangesAsync();
        _context.Entry(existingRule).State = EntityState.Detached;

        var updatedRule = new Rule
        {
            Id = 1,
            Title = "Updated Title",
            MarkdownContent = "Updated Content",
            Description = "Test",
            Category = "Grammar",
            UserId = _testUserId
        };

        // Act
        var result = await _controller.UpdateRule(1, updatedRule);

        // Assert
        result.Should().BeOfType<NoContentResult>();
        
        var rule = await _context.Rules.FindAsync(1);
        rule!.Title.Should().Be("Updated Title");
    }

    [Fact]
    public async Task UpdateRule_WithMismatchedId_ReturnsBadRequest()
    {
        // Arrange
        var rule = new Rule { Id = 2, Title = "Test", MarkdownContent = "Content", Description = "Test", Category = "Grammar" };

        // Act
        var result = await _controller.UpdateRule(1, rule);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task UpdateRule_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var rule = new Rule { Id = 999, Title = "Test", MarkdownContent = "Content", Description = "Test", Category = "Grammar" };

        // Act
        var result = await _controller.UpdateRule(999, rule);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task UpdateRule_WithOtherUsersRule_ReturnsForbid()
    {
        // Arrange
        var existingRule = new Rule
        {
            Id = 1,
            Title = "Original",
            MarkdownContent = "Content",
            Description = "Test",
            Category = "Grammar",
            UserId = 999 // Different user
        };
        _context.Rules.Add(existingRule);
        await _context.SaveChangesAsync();
        _context.Entry(existingRule).State = EntityState.Detached;

        var updateRule = new Rule { Id = 1, Title = "Update", MarkdownContent = "Content", Description = "Test", Category = "Grammar" };

        // Act
        var result = await _controller.UpdateRule(1, updateRule);

        // Assert
        result.Should().BeOfType<ForbidResult>();
    }

    #endregion
}
