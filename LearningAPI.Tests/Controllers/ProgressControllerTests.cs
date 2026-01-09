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
using Xunit;

namespace LearningAPI.Tests.Controllers;

public class ProgressControllerTests : IDisposable
{
    private readonly ApiDbContext _context;
    private readonly Mock<ILogger<ProgressController>> _loggerMock;
    private readonly ProgressController _controller;
    private readonly int _testUserId = 1;

    public ProgressControllerTests()
    {
        _context = TestDbContextFactory.CreateInMemoryContext();
        _loggerMock = new Mock<ILogger<ProgressController>>();
        
        _controller = new ProgressController(_context, _loggerMock.Object);
        SetupUserContext(_testUserId);
    }

    private void SetupUserContext(int userId)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, "Student")
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

    private async Task<Word> CreateTestWord()
    {
        var dictionary = new Dictionary
        {
            Name = "Test",
            Description = "Test",
            LanguageFrom = "English",
            LanguageTo = "Russian",
            UserId = _testUserId,
            Words = new List<Word>()
        };
        _context.Dictionaries.Add(dictionary);
        await _context.SaveChangesAsync();

        var word = new Word
        {
            OriginalWord = "Hello",
            Translation = "Привет",
            Transcription = "[h??lo?]",
            Example = "Hello, world!",
            DictionaryId = dictionary.Id,
            UserId = _testUserId,
            AddedAt = DateTime.UtcNow
        };
        _context.Words.Add(word);
        await _context.SaveChangesAsync();

        return word;
    }

    [Fact]
    public async Task UpdateProgress_WithValidWord_CreatesNewProgress()
    {
        // Arrange
        var word = await CreateTestWord();
        var request = new UpdateProgressRequest
        {
            WordId = word.Id,
            Quality = ResponseQuality.Good
        };

        // Act
        var result = await _controller.UpdateProgress(request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var progress = okResult.Value as LearningProgress;
        progress.Should().NotBeNull();
        progress!.WordId.Should().Be(word.Id);
        progress.KnowledgeLevel.Should().Be(1);
        progress.TotalAttempts.Should().Be(1);
        progress.CorrectAnswers.Should().Be(1);
    }

    [Fact]
    public async Task UpdateProgress_WithNonExistentWord_ReturnsNotFound()
    {
        // Arrange
        var request = new UpdateProgressRequest
        {
            WordId = 999,
            Quality = ResponseQuality.Good
        };

        // Act
        var result = await _controller.UpdateProgress(request);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task UpdateProgress_WithAgainQuality_ResetsKnowledgeLevel()
    {
        // Arrange
        var word = await CreateTestWord();
        
        // Create existing progress with level 3
        var existingProgress = new LearningProgress
        {
            UserId = _testUserId,
            WordId = word.Id,
            KnowledgeLevel = 3,
            TotalAttempts = 5,
            CorrectAnswers = 4,
            LastPracticed = DateTime.UtcNow.AddDays(-1),
            NextReview = DateTime.UtcNow
        };
        _context.LearningProgresses.Add(existingProgress);
        await _context.SaveChangesAsync();

        var request = new UpdateProgressRequest
        {
            WordId = word.Id,
            Quality = ResponseQuality.Again
        };

        // Act
        var result = await _controller.UpdateProgress(request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var progress = okResult.Value as LearningProgress;
        progress.Should().NotBeNull();
        progress!.KnowledgeLevel.Should().Be(0); // Reset to 0
        progress.TotalAttempts.Should().Be(6);
    }

    [Fact]
    public async Task UpdateProgress_WithEasyQuality_IncreasesKnowledgeLevelByTwo()
    {
        // Arrange
        var word = await CreateTestWord();
        var request = new UpdateProgressRequest
        {
            WordId = word.Id,
            Quality = ResponseQuality.Easy
        };

        // Act
        var result = await _controller.UpdateProgress(request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var progress = okResult.Value as LearningProgress;
        progress.Should().NotBeNull();
        progress!.KnowledgeLevel.Should().Be(2); // +2 levels
    }

    [Fact]
    public async Task GetStats_ReturnsUserStats()
    {
        // Arrange
        var word = await CreateTestWord();
        
        var progress = new LearningProgress
        {
            UserId = _testUserId,
            WordId = word.Id,
            KnowledgeLevel = 4,
            TotalAttempts = 10,
            CorrectAnswers = 8,
            LastPracticed = DateTime.UtcNow,
            NextReview = DateTime.UtcNow.AddDays(14)
        };
        _context.LearningProgresses.Add(progress);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetStats();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var stats = okResult.Value as DashboardStats;
        stats.Should().NotBeNull();
        stats!.LearnedWords.Should().Be(1);
        stats.TotalWords.Should().Be(1);
        stats.AverageSuccessRate.Should().BeApproximately(0.8, 0.01);
    }

    [Fact]
    public async Task GetStats_WithNoProgress_ReturnsEmptyStats()
    {
        // Act
        var result = await _controller.GetStats();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var stats = okResult.Value as DashboardStats;
        stats.Should().NotBeNull();
        stats!.LearnedWords.Should().Be(0);
        stats.TotalWords.Should().Be(0);
        stats.AverageSuccessRate.Should().Be(0);
    }
}
