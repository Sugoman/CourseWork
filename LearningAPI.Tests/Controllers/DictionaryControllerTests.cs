using FluentAssertions;
using LearningAPI.Controllers;
using LearningAPI.Services;
using LearningAPI.Tests.Helpers;
using LearningTrainerShared.Context;
using LearningTrainerShared.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;
using Xunit;

namespace LearningAPI.Tests.Controllers;

public class DictionaryControllerTests : IDisposable
{
    private readonly ApiDbContext _context;
    private readonly Mock<ILogger<DictionaryController>> _loggerMock;
    private readonly DictionaryController _controller;
    private readonly int _testUserId = 1;

    public DictionaryControllerTests()
    {
        _context = TestDbContextFactory.CreateInMemoryContext();
        _loggerMock = new Mock<ILogger<DictionaryController>>();
        var cacheMock = new Mock<IDistributedCache>();

        // Ensure cache always returns null (MISS)
        cacheMock.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        var dictionaryService = new DictionaryService(_context, cacheMock.Object);
        _controller = new DictionaryController(dictionaryService, _loggerMock.Object);
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

        var httpContext = new DefaultHttpContext
        {
            User = claimsPrincipal
        };
        
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public async Task GetDictionaries_ReturnsUserDictionaries()
    {
        // Arrange
        var role = TestDataSeeder.CreateTeacherRole();
        _context.Roles.Add(role);
        
        var user = new User 
        { 
            Id = _testUserId, 
            Login = "testuser", 
            PasswordHash = "hash",
            Role = role 
        };
        _context.Users.Add(user);
        
        var dictionary = TestDataSeeder.CreateTestDictionary(_testUserId, "My Dictionary");
        _context.Dictionaries.Add(dictionary);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetDictionaries();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value;
        
        response.Should().NotBeNull();
        var dataProperty = response!.GetType().GetProperty("data");
        var dictionaries = dataProperty?.GetValue(response) as IEnumerable<Dictionary>;
        dictionaries.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetDictionaryById_WithValidId_ReturnsDictionary()
    {
        // Arrange
        var role = TestDataSeeder.CreateTeacherRole();
        _context.Roles.Add(role);
        
        var dictionary = new Dictionary
        {
            Id = 1,
            Name = "Test Dictionary",
            Description = "Test",
            LanguageFrom = "English",
            LanguageTo = "Russian",
            UserId = _testUserId,
            Words = new List<Word>()
        };
        _context.Dictionaries.Add(dictionary);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetDictionaryById(1);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedDictionary = okResult.Value as Dictionary;
        returnedDictionary.Should().NotBeNull();
        returnedDictionary!.Name.Should().Be("Test Dictionary");
    }

    [Fact]
    public async Task GetDictionaryById_WithInvalidId_ReturnsNotFound()
    {
        // Act
        var result = await _controller.GetDictionaryById(999);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetDictionaryById_WithOtherUserId_ReturnsNotFound()
    {
        // Arrange
        var dictionary = new Dictionary
        {
            Id = 1,
            Name = "Other User Dictionary",
            Description = "Test",
            LanguageFrom = "English",
            LanguageTo = "Russian",
            UserId = 999, // Different user
            Words = new List<Word>()
        };
        _context.Dictionaries.Add(dictionary);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetDictionaryById(1);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task AddDictionary_WithValidData_ReturnsCreatedDictionary()
    {
        // Arrange
        var role = TestDataSeeder.CreateTeacherRole();
        _context.Roles.Add(role);
        await _context.SaveChangesAsync();

        var request = new CreateDictionaryRequest
        {
            Name = "New Dictionary",
            Description = "New description",
            LanguageFrom = "English",
            LanguageTo = "German"
        };

        // Act
        var result = await _controller.AddDictionary(request);

        // Assert
        result.Should().BeOfType<CreatedAtActionResult>();
        
        var createdDictionary = await _context.Dictionaries.FirstOrDefaultAsync(d => d.Name == "New Dictionary");
        createdDictionary.Should().NotBeNull();
        createdDictionary!.UserId.Should().Be(_testUserId);
    }

    [Fact]
    public async Task DeleteDictionary_WithValidId_ReturnsNoContent()
    {
        // Arrange
        var dictionary = new Dictionary
        {
            Id = 1,
            Name = "To Delete",
            Description = "Test",
            LanguageFrom = "English",
            LanguageTo = "Russian",
            UserId = _testUserId,
            Words = new List<Word>()
        };
        _context.Dictionaries.Add(dictionary);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.DeleteDictionary(1);

        // Assert
        result.Should().BeOfType<NoContentResult>();
        
        var deletedDictionary = await _context.Dictionaries.FindAsync(1);
        deletedDictionary.Should().BeNull();
    }

    [Fact]
    public async Task DeleteDictionary_WithOtherUserId_ReturnsForbid()
    {
        // Arrange
        var dictionary = new Dictionary
        {
            Id = 1,
            Name = "Other User Dictionary",
            Description = "Test",
            LanguageFrom = "English",
            LanguageTo = "Russian",
            UserId = 999, // Different user
            Words = new List<Word>()
        };
        _context.Dictionaries.Add(dictionary);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.DeleteDictionary(1);

        // Assert — service returns NotFound for both missing and unauthorized (security: don't reveal existence)
        result.Should().BeOfType<NotFoundResult>();
    }
}
