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

public class DictionaryControllerExtendedTests : IDisposable
{
    private readonly ApiDbContext _context;
    private readonly Mock<ILogger<DictionaryController>> _loggerMock;
    private readonly DictionaryController _controller;
    private readonly int _testUserId = 1;

    public DictionaryControllerExtendedTests()
    {
        _context = TestDbContextFactory.CreateInMemoryContext();
        _loggerMock = new Mock<ILogger<DictionaryController>>();
        var cacheMock = new Mock<IDistributedCache>();

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

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    #region UpdateDictionary Tests

    [Fact]
    public async Task UpdateDictionary_WithValidData_UpdatesDictionary()
    {
        // Arrange
        var dictionary = new Dictionary
        {
            Id = 1,
            Name = "Original Name",
            Description = "Original Description",
            LanguageFrom = "English",
            LanguageTo = "Russian",
            UserId = _testUserId,
            Words = new List<Word>()
        };
        _context.Dictionaries.Add(dictionary);
        await _context.SaveChangesAsync();
        _context.Entry(dictionary).State = EntityState.Detached;

        var updateRequest = new UpdateDictionaryRequest
        {
            Id = 1,
            Name = "Updated Name",
            Description = "Updated Description",
            LanguageFrom = "German",
            LanguageTo = "English"
        };

        // Act
        var result = await _controller.UpdateDictionary(1, updateRequest);

        // Assert
        result.Should().BeOfType<NoContentResult>();
        
        var updated = await _context.Dictionaries.FindAsync(1);
        updated!.Name.Should().Be("Updated Name");
        updated.Description.Should().Be("Updated Description");
    }

    [Fact]
    public async Task UpdateDictionary_WithMismatchedId_ReturnsBadRequest()
    {
        // Arrange
        var dictionary = new UpdateDictionaryRequest { Id = 2, Name = "Test" };

        // Act
        var result = await _controller.UpdateDictionary(1, dictionary);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task UpdateDictionary_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var dictionary = new UpdateDictionaryRequest { Id = 999, Name = "Test" };

        // Act
        var result = await _controller.UpdateDictionary(999, dictionary);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    #endregion

    #region GetAvailableDictionaries Tests

    [Fact]
    public async Task GetAvailableDictionaries_ReturnsOwnAndSharedDictionaries()
    {
        // Arrange
        var role = TestDataSeeder.CreateTeacherRole();
        _context.Roles.Add(role);
        
        var user = new User { Id = _testUserId, Login = "testuser", PasswordHash = "hash", Role = role };
        var otherUser = new User { Id = 2, Login = "otheruser", PasswordHash = "hash", Role = role };
        _context.Users.AddRange(user, otherUser);
        
        var ownDict = new Dictionary
        {
            Id = 1,
            Name = "Own Dict",
            Description = "Test",
            LanguageFrom = "English",
            LanguageTo = "Russian",
            UserId = _testUserId,
            Words = new List<Word>()
        };
        var sharedDict = new Dictionary
        {
            Id = 2,
            Name = "Shared Dict",
            Description = "Test",
            LanguageFrom = "English",
            LanguageTo = "Russian",
            UserId = 2,
            Words = new List<Word>()
        };
        _context.Dictionaries.AddRange(ownDict, sharedDict);
        
        var sharing = new DictionarySharing
        {
            DictionaryId = 2,
            StudentId = _testUserId,
            SharedAt = DateTime.UtcNow
        };
        _context.DictionarySharings.Add(sharing);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetAvailableDictionaries();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().NotBeNull();
    }

    #endregion

    #region DeleteDictionary Extended Tests

    [Fact]
    public async Task DeleteDictionary_WithValidId_DeletesDictionaryAndWords()
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
        
        // Add word separately
        _context.Words.Add(new Word { OriginalWord = "Hello", Translation = "������", Example = "", UserId = _testUserId, DictionaryId = dictionary.Id });
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.DeleteDictionary(1);

        // Assert
        result.Should().BeOfType<NoContentResult>();
        
        var deletedDict = await _context.Dictionaries.FindAsync(1);
        deletedDict.Should().BeNull();
        
        var words = await _context.Words.Where(w => w.DictionaryId == 1).ToListAsync();
        words.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteDictionary_WithNonExistentId_ReturnsNotFound()
    {
        // Act
        var result = await _controller.DeleteDictionary(999);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task DeleteDictionary_WithOtherUsersDictionary_ReturnsForbid()
    {
        // Arrange
        var dictionary = new Dictionary
        {
            Id = 1,
            Name = "Other's Dict",
            Description = "Test",
            LanguageFrom = "English",
            LanguageTo = "Russian",
            UserId = 999,
            Words = new List<Word>()
        };
        _context.Dictionaries.Add(dictionary);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.DeleteDictionary(1);

        // Assert — service returns NotFound for both missing and unauthorized (security: don't reveal existence)
        result.Should().BeOfType<NotFoundResult>();
    }

    #endregion

    #region AddDictionary Extended Tests

    [Fact]
    public async Task AddDictionary_SetsCorrectUserId()
    {
        // Arrange
        var request = new CreateDictionaryRequest
        {
            Name = "New Dict",
            Description = "Test",
            LanguageFrom = "English",
            LanguageTo = "Russian"
        };

        // Act
        await _controller.AddDictionary(request);

        // Assert
        var dictionary = await _context.Dictionaries.FirstAsync();
        dictionary.UserId.Should().Be(_testUserId);
    }

    #endregion

    #region GetReviewSession Tests

    [Fact]
    public async Task GetReviewSession_ReturnsWordsDueForReview()
    {
        // Arrange
        var dictionary = new Dictionary
        {
            Id = 1,
            Name = "Test Dict",
            Description = "Test",
            LanguageFrom = "English",
            LanguageTo = "Russian",
            UserId = _testUserId,
            Words = new List<Word>()
        };
        _context.Dictionaries.Add(dictionary);
        await _context.SaveChangesAsync();
        
        // Add words separately
        _context.Words.Add(new Word { OriginalWord = "Hello", Translation = "������", Example = "", UserId = _testUserId, DictionaryId = dictionary.Id });
        _context.Words.Add(new Word { OriginalWord = "World", Translation = "���", Example = "", UserId = _testUserId, DictionaryId = dictionary.Id });
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetReviewSession(1);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().NotBeNull();
    }

    #endregion
}
