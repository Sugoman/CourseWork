using FluentAssertions;
using LearningAPI.Controllers;
using LearningAPI.Tests.Helpers;
using LearningTrainer.Context;
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

public class ImportControllerTests : IDisposable
{
    private readonly ApiDbContext _context;
    private readonly Mock<ILogger<ImportController>> _loggerMock;
    private readonly ImportController _controller;
    private readonly int _testUserId = 1;

    public ImportControllerTests()
    {
        _context = TestDbContextFactory.CreateInMemoryContext();
        _loggerMock = new Mock<ILogger<ImportController>>();
        _controller = new ImportController(_context, _loggerMock.Object);
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

    private IFormFile CreateJsonFile(object content, string fileName = "test.json")
    {
        var json = JsonSerializer.Serialize(content);
        var bytes = Encoding.UTF8.GetBytes(json);
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/json"
        };
    }

    private IFormFile CreateCsvFile(string content, string fileName = "test.csv")
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/csv"
        };
    }

    #region ImportFromJson Tests

    [Fact]
    public async Task ImportFromJson_WithValidFile_CreatesDictionary()
    {
        // Arrange
        var importData = new
        {
            Name = "Imported Dictionary",
            Description = "Test import",
            LanguageFrom = "English",
            LanguageTo = "Russian",
            Words = new[]
            {
                new { Original = "Hello", Translation = "Привет", PartOfSpeech = "[h??lo?]", Example = "Hello world" },
                new { Original = "World", Translation = "Мир", PartOfSpeech = "[w??rld]", Example = "Hello world" }
            }
        };
        var file = CreateJsonFile(importData);

        // Act
        var result = await _controller.ImportFromJson(file);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        
        var dictionaries = await _context.Dictionaries.Include(d => d.Words).ToListAsync();
        dictionaries.Should().HaveCount(1);
        dictionaries.First().Name.Should().Be("Imported Dictionary");
        dictionaries.First().Words.Should().HaveCount(2);
    }

    [Fact]
    public async Task ImportFromJson_WithNullFile_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.ImportFromJson(null!);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ImportFromJson_WithEmptyFile_ReturnsBadRequest()
    {
        // Arrange
        var stream = new MemoryStream();
        var file = new FormFile(stream, 0, 0, "file", "empty.json");

        // Act
        var result = await _controller.ImportFromJson(file);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ImportFromJson_WithNonJsonFile_ReturnsBadRequest()
    {
        // Arrange
        var bytes = Encoding.UTF8.GetBytes("not json");
        var stream = new MemoryStream(bytes);
        var file = new FormFile(stream, 0, bytes.Length, "file", "test.txt");

        // Act
        var result = await _controller.ImportFromJson(file);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ImportFromJson_WithInvalidJson_ReturnsBadRequest()
    {
        // Arrange
        var bytes = Encoding.UTF8.GetBytes("{ invalid json }");
        var stream = new MemoryStream(bytes);
        var file = new FormFile(stream, 0, bytes.Length, "file", "test.json");

        // Act
        var result = await _controller.ImportFromJson(file);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ImportFromJson_WithMinimalData_UsesDefaults()
    {
        // Arrange
        var importData = new
        {
            Words = new[]
            {
                new { Original = "Test", Translation = "Тест" }
            }
        };
        var file = CreateJsonFile(importData);

        // Act
        var result = await _controller.ImportFromJson(file);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        
        var dictionary = await _context.Dictionaries.FirstAsync();
        dictionary.Name.Should().Be("Imported Dictionary");
        dictionary.LanguageFrom.Should().Be("English");
        dictionary.LanguageTo.Should().Be("Russian");
    }

    [Fact]
    public async Task ImportFromJson_WithNoWords_CreatesEmptyDictionary()
    {
        // Arrange
        var importData = new
        {
            Name = "Empty Import",
            Description = "No words"
        };
        var file = CreateJsonFile(importData);

        // Act
        var result = await _controller.ImportFromJson(file);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        
        var dictionary = await _context.Dictionaries.Include(d => d.Words).FirstAsync();
        dictionary.Words.Should().BeEmpty();
    }

    [Fact]
    public async Task ImportFromJson_SetsCorrectUserId()
    {
        // Arrange
        var importData = new
        {
            Name = "Test",
            Words = new[]
            {
                new { Original = "Test", Translation = "Тест" }
            }
        };
        var file = CreateJsonFile(importData);

        // Act
        await _controller.ImportFromJson(file);

        // Assert
        var dictionary = await _context.Dictionaries.Include(d => d.Words).FirstAsync();
        dictionary.UserId.Should().Be(_testUserId);
        dictionary.Words.All(w => w.UserId == _testUserId).Should().BeTrue();
    }

    #endregion

    #region ImportFromCsv Tests

    [Fact]
    public async Task ImportFromCsv_WithValidFile_CreatesDictionary()
    {
        // Arrange
        var csvContent = "Original,Translation,Example\nHello,Привет,Hello world\nWorld,Мир,Hello world";
        var file = CreateCsvFile(csvContent);

        // Act
        var result = await _controller.ImportFromCsv(file, "Test Dictionary", "English", "Russian");

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        
        var dictionaries = await _context.Dictionaries.Include(d => d.Words).ToListAsync();
        dictionaries.Should().HaveCount(1);
        dictionaries.First().Words.Should().HaveCount(2);
    }

    [Fact]
    public async Task ImportFromCsv_WithNullFile_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.ImportFromCsv(null!, "Test", "English", "Russian");

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ImportFromCsv_WithNonCsvFile_ReturnsBadRequest()
    {
        // Arrange
        var bytes = Encoding.UTF8.GetBytes("test");
        var stream = new MemoryStream(bytes);
        var file = new FormFile(stream, 0, bytes.Length, "file", "test.txt");

        // Act
        var result = await _controller.ImportFromCsv(file, "Test", "English", "Russian");

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    #endregion
}
