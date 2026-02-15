using FluentAssertions;
using LearningAPI.Controllers;
using LearningAPI.Tests.Helpers;
using LearningTrainerShared.Context;
using LearningTrainerShared.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Xunit;

namespace LearningAPI.Tests.Controllers;

public class ExportControllerTests : IDisposable
{
    private readonly ApiDbContext _context;
    private readonly Mock<ILogger<ExportController>> _loggerMock;
    private readonly ExportController _controller;
    private readonly int _testUserId = 1;

    public ExportControllerTests()
    {
        _context = TestDbContextFactory.CreateInMemoryContext();
        _loggerMock = new Mock<ILogger<ExportController>>();
        _controller = new ExportController(_context, _loggerMock.Object);
        SetupUserContext(_testUserId);
    }

    private void SetupUserContext(int userId)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, "Teacher")
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

    private async Task<Dictionary> CreateTestDictionary()
    {
        var dictionary = new Dictionary
        {
            Name = "Test Dictionary",
            Description = "Test description",
            LanguageFrom = "English",
            LanguageTo = "Russian",
            UserId = _testUserId,
            Words = new List<Word>()
        };
        _context.Dictionaries.Add(dictionary);
        await _context.SaveChangesAsync();
        
        // Add words after dictionary is saved
        _context.Words.Add(new Word { OriginalWord = "Hello", Translation = "������", Transcription = "[h??lo?]", Example = "Hello world", UserId = _testUserId, DictionaryId = dictionary.Id });
        _context.Words.Add(new Word { OriginalWord = "World", Translation = "���", Transcription = "[w??rld]", Example = "Hello world", UserId = _testUserId, DictionaryId = dictionary.Id });
        await _context.SaveChangesAsync();
        
        return dictionary;
    }

    #region ExportAsJson Tests

    [Fact]
    public async Task ExportAsJson_WithValidDictionary_ReturnsJsonFile()
    {
        // Arrange
        var dictionary = await CreateTestDictionary();

        // Act
        var result = await _controller.ExportAsJson(dictionary.Id);

        // Assert
        var fileResult = result.Should().BeOfType<FileContentResult>().Subject;
        fileResult.ContentType.Should().Be("application/json");
        fileResult.FileDownloadName.Should().Contain("Test Dictionary");
        fileResult.FileDownloadName.Should().EndWith(".json");
    }

    [Fact]
    public async Task ExportAsJson_WithValidDictionary_ContainsCorrectData()
    {
        // Arrange
        var dictionary = await CreateTestDictionary();
        
        // Reload to ensure words are included
        var loadedDictionary = await _context.Dictionaries
            .Include(d => d.Words)
            .FirstAsync(d => d.Id == dictionary.Id);

        // Act
        var result = await _controller.ExportAsJson(loadedDictionary.Id);

        // Assert
        var fileResult = result.Should().BeOfType<FileContentResult>().Subject;
        var json = Encoding.UTF8.GetString(fileResult.FileContents);
        
        json.Should().Contain("Test Dictionary");
        json.Should().Contain("Hello");
        json.Should().Contain("English");
        json.Should().Contain("Russian");
    }

    [Fact]
    public async Task ExportAsJson_WithNonExistentDictionary_ReturnsNotFound()
    {
        // Act
        var result = await _controller.ExportAsJson(999);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task ExportAsJson_WithOtherUsersDictionary_ReturnsNotFound()
    {
        // Arrange
        var dictionary = new Dictionary
        {
            Name = "Other User Dict",
            Description = "Test",
            LanguageFrom = "English",
            LanguageTo = "Russian",
            UserId = 999, // Different user
            Words = new List<Word>()
        };
        _context.Dictionaries.Add(dictionary);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.ExportAsJson(dictionary.Id);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task ExportAsJson_WithEmptyDictionary_ReturnsJsonWithEmptyWords()
    {
        // Arrange
        var dictionary = new Dictionary
        {
            Name = "Empty Dictionary",
            Description = "No words",
            LanguageFrom = "English",
            LanguageTo = "Russian",
            UserId = _testUserId,
            Words = new List<Word>()
        };
        _context.Dictionaries.Add(dictionary);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.ExportAsJson(dictionary.Id);

        // Assert
        var fileResult = result.Should().BeOfType<FileContentResult>().Subject;
        var json = Encoding.UTF8.GetString(fileResult.FileContents);
        json.Should().Contain("Empty Dictionary");
    }

    #endregion

    #region ExportAsCsv Tests

    [Fact]
    public async Task ExportAsCsv_WithValidDictionary_ReturnsCsvFile()
    {
        // Arrange
        var dictionary = await CreateTestDictionary();

        // Act
        var result = await _controller.ExportAsCsv(dictionary.Id);

        // Assert
        var fileResult = result.Should().BeOfType<FileContentResult>().Subject;
        fileResult.ContentType.Should().Be("text/csv");
        fileResult.FileDownloadName.Should().Contain("Test Dictionary");
        fileResult.FileDownloadName.Should().EndWith(".csv");
    }

    [Fact]
    public async Task ExportAsCsv_WithValidDictionary_ContainsCorrectData()
    {
        // Arrange
        var dictionary = await CreateTestDictionary();

        // Act
        var result = await _controller.ExportAsCsv(dictionary.Id);

        // Assert
        var fileResult = result.Should().BeOfType<FileContentResult>().Subject;
        var csv = Encoding.UTF8.GetString(fileResult.FileContents);
        
        csv.Should().Contain("Hello");
        csv.Should().Contain("������");
        csv.Should().Contain("World");
        csv.Should().Contain("���");
    }

    [Fact]
    public async Task ExportAsCsv_WithNonExistentDictionary_ReturnsNotFound()
    {
        // Act
        var result = await _controller.ExportAsCsv(999);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task ExportAsCsv_WithOtherUsersDictionary_ReturnsNotFound()
    {
        // Arrange
        var dictionary = new Dictionary
        {
            Name = "Other User Dict",
            Description = "Test",
            LanguageFrom = "English",
            LanguageTo = "Russian",
            UserId = 999,
            Words = new List<Word>()
        };
        _context.Dictionaries.Add(dictionary);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.ExportAsCsv(dictionary.Id);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    #endregion
}
