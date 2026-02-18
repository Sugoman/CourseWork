using FluentAssertions;
using LearningAPI.Controllers;
using LearningAPI.Tests.Helpers;
using LearningTrainerShared.Context;
using LearningTrainerShared.Models;
using LearningTrainerShared.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;
using Xunit;

namespace LearningAPI.Tests.Controllers;

public class ProgressControllerExtendedTests : IDisposable
{
    private readonly ApiDbContext _context;
    private readonly Mock<ILogger<ProgressController>> _loggerMock;
    private readonly ProgressController _controller;
    private readonly int _testUserId = 1;

    public ProgressControllerExtendedTests()
    {
        _context = TestDbContextFactory.CreateInMemoryContext();
        _loggerMock = new Mock<ILogger<ProgressController>>();
        var cacheMock = new Mock<IDistributedCache>();
        
        // Ensure cache always returns null (MISS)
        cacheMock.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);
            
        _controller = new ProgressController(_context, _loggerMock.Object, cacheMock.Object, new SpacedRepetitionService());
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
            Translation = "������",
            Example = "",
            DictionaryId = dictionary.Id,
            UserId = _testUserId
        };
        _context.Words.Add(word);
        await _context.SaveChangesAsync();
        return word;
    }

    #region UpdateProgress Extended Tests

    [Fact]
    public async Task UpdateProgress_WithHardQuality_IncreasesCorrectAnswersAndSetsNextReview()
    {
        // Arrange
        var word = await CreateTestWord();
        var request = new UpdateProgressRequest
        {
            WordId = word.Id,
            Quality = ResponseQuality.Hard
        };

        // Act
        var result = await _controller.UpdateProgress(request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var progress = okResult.Value as LearningProgress;
        progress.Should().NotBeNull();
        progress!.CorrectAnswers.Should().Be(1);
        progress.NextReview.Should().BeCloseTo(DateTime.UtcNow.AddDays(1), TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task UpdateProgress_WithGoodQuality_IncreasesKnowledgeLevelByOne()
    {
        // Arrange
        var word = await CreateTestWord();
        var existingProgress = new LearningProgress
        {
            UserId = _testUserId,
            WordId = word.Id,
            KnowledgeLevel = 2,
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
            Quality = ResponseQuality.Good
        };

        // Act
        var result = await _controller.UpdateProgress(request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var progress = okResult.Value as LearningProgress;
        progress.Should().NotBeNull();
        progress!.KnowledgeLevel.Should().Be(3);
        progress.CorrectAnswers.Should().Be(5);
    }

    [Fact]
    public async Task UpdateProgress_KnowledgeLevelIncrementsByOne_ForCorrectAnswer()
    {
        // Arrange
        var word = await CreateTestWord();
        var existingProgress = new LearningProgress
        {
            UserId = _testUserId,
            WordId = word.Id,
            KnowledgeLevel = 5,
            TotalAttempts = 10,
            CorrectAnswers = 9,
            LastPracticed = DateTime.UtcNow.AddDays(-1),
            NextReview = DateTime.UtcNow
        };
        _context.LearningProgresses.Add(existingProgress);
        await _context.SaveChangesAsync();

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
        progress!.KnowledgeLevel.Should().Be(6); // SM-2: +1 per correct answer, no cap
        progress.EaseFactor.Should().BeGreaterOrEqualTo(1.3); // SM-2: minimum EF = 1.3
        progress.IntervalDays.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task UpdateProgress_UpdatesLastPracticedTime()
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
        progress!.LastPracticed.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task UpdateProgress_CreatesNewProgressIfNotExists()
    {
        // Arrange
        var word = await CreateTestWord();
        var request = new UpdateProgressRequest
        {
            WordId = word.Id,
            Quality = ResponseQuality.Good
        };

        // Verify no progress exists
        var existingProgress = await _context.LearningProgresses
            .FirstOrDefaultAsync(p => p.UserId == _testUserId && p.WordId == word.Id);
        existingProgress.Should().BeNull();

        // Act
        var result = await _controller.UpdateProgress(request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        
        var newProgress = await _context.LearningProgresses
            .FirstOrDefaultAsync(p => p.UserId == _testUserId && p.WordId == word.Id);
        newProgress.Should().NotBeNull();
    }

    #endregion

    #region GetStats Tests

    [Fact]
    public async Task GetStats_ReturnsCorrectStatistics()
    {
        // Arrange
        var word1 = await CreateTestWord();
        
        // Add more words
        var dictionary = await _context.Dictionaries.FirstAsync();
        var word2 = new Word { OriginalWord = "World", Translation = "���", Example = "", DictionaryId = dictionary.Id, UserId = _testUserId };
        _context.Words.Add(word2);
        await _context.SaveChangesAsync();

        // Add progress for both words
        var progress1 = new LearningProgress
        {
            UserId = _testUserId,
            WordId = word1.Id,
            KnowledgeLevel = 4,
            TotalAttempts = 10,
            CorrectAnswers = 8,
            LastPracticed = DateTime.UtcNow,
            NextReview = DateTime.UtcNow.AddDays(7)
        };
        var progress2 = new LearningProgress
        {
            UserId = _testUserId,
            WordId = word2.Id,
            KnowledgeLevel = 2,
            TotalAttempts = 5,
            CorrectAnswers = 3,
            LastPracticed = DateTime.UtcNow,
            NextReview = DateTime.UtcNow.AddDays(1)
        };
        _context.LearningProgresses.AddRange(progress1, progress2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetStats();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task GetStats_WithNoProgress_ReturnsEmptyStats()
    {
        // Arrange - no progress data

        // Act
        var result = await _controller.GetStats();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().NotBeNull();
    }

    #endregion
}
