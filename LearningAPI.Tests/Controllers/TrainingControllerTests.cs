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

public class TrainingControllerTests : IDisposable
{
    private readonly ApiDbContext _context;
    private readonly Mock<ILogger<TrainingController>> _loggerMock;
    private readonly TrainingController _controller;
    private readonly int _testUserId = 1;

    public TrainingControllerTests()
    {
        _context = TestDbContextFactory.CreateInMemoryContext();
        _loggerMock = new Mock<ILogger<TrainingController>>();
        
        _controller = new TrainingController(_context, _loggerMock.Object);
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

    private async Task<Dictionary> CreateTestDictionary(string name = "Test Dictionary")
    {
        var dictionary = new Dictionary
        {
            Name = name,
            Description = "Test description",
            LanguageFrom = "English",
            LanguageTo = "Russian",
            UserId = _testUserId,
            Words = new List<Word>()
        };
        _context.Dictionaries.Add(dictionary);
        await _context.SaveChangesAsync();
        return dictionary;
    }

    private async Task<Word> CreateTestWord(int dictionaryId, string original = "hello", string translation = "привет")
    {
        var word = new Word
        {
            OriginalWord = original,
            Translation = translation,
            Transcription = "[həˈloʊ]",
            Example = "Hello, world!",
            DictionaryId = dictionaryId,
            UserId = _testUserId,
            AddedAt = DateTime.UtcNow
        };
        _context.Words.Add(word);
        await _context.SaveChangesAsync();
        return word;
    }

    private async Task<LearningProgress> CreateTestProgress(int wordId, int knowledgeLevel = 0, DateTime? nextReview = null)
    {
        var progress = new LearningProgress
        {
            UserId = _testUserId,
            WordId = wordId,
            KnowledgeLevel = knowledgeLevel,
            NextReview = nextReview ?? DateTime.UtcNow.AddDays(-1), // По умолчанию - к повторению
            LastPracticed = DateTime.UtcNow.AddDays(-1),
            TotalAttempts = 5,
            CorrectAnswers = 3
        };
        _context.LearningProgresses.Add(progress);
        await _context.SaveChangesAsync();
        return progress;
    }

    #region GetDailyPlan Tests

    [Fact]
    public async Task GetDailyPlan_WithNoWords_ReturnsEmptyPlan()
    {
        // Act
        var result = await _controller.GetDailyPlan();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var plan = okResult.Value.Should().BeOfType<DailyPlanDto>().Subject;
        
        plan.ReviewWords.Should().BeEmpty();
        plan.NewWords.Should().BeEmpty();
        plan.DifficultWords.Should().BeEmpty();
        plan.Stats.TotalReviewCount.Should().Be(0);
        plan.Stats.TotalNewCount.Should().Be(0);
    }

    [Fact]
    public async Task GetDailyPlan_WithNewWords_ReturnsNewWordsInPlan()
    {
        // Arrange
        var dictionary = await CreateTestDictionary();
        await CreateTestWord(dictionary.Id, "apple", "яблоко");
        await CreateTestWord(dictionary.Id, "book", "книга");
        await CreateTestWord(dictionary.Id, "car", "машина");

        // Act
        var result = await _controller.GetDailyPlan();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var plan = okResult.Value.Should().BeOfType<DailyPlanDto>().Subject;
        
        plan.NewWords.Should().HaveCount(3);
        plan.Stats.TotalNewCount.Should().Be(3);
        plan.ReviewWords.Should().BeEmpty();
    }

    [Fact]
    public async Task GetDailyPlan_WithWordsToReview_ReturnsReviewWords()
    {
        // Arrange
        var dictionary = await CreateTestDictionary();
        var word1 = await CreateTestWord(dictionary.Id, "apple", "яблоко");
        var word2 = await CreateTestWord(dictionary.Id, "book", "книга");
        
        // Создаём прогресс с датой повторения в прошлом
        await CreateTestProgress(word1.Id, knowledgeLevel: 2, nextReview: DateTime.UtcNow.AddDays(-1));
        await CreateTestProgress(word2.Id, knowledgeLevel: 1, nextReview: DateTime.UtcNow.AddHours(-2));

        // Act
        var result = await _controller.GetDailyPlan();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var plan = okResult.Value.Should().BeOfType<DailyPlanDto>().Subject;
        
        plan.ReviewWords.Should().HaveCount(2);
        plan.Stats.TotalReviewCount.Should().Be(2);
    }

    [Fact]
    public async Task GetDailyPlan_WithDifficultWords_ReturnsDifficultWords()
    {
        // Arrange
        var dictionary = await CreateTestDictionary();
        var word = await CreateTestWord(dictionary.Id, "difficult", "сложный");
        
        // Создаём прогресс со сброшенным уровнем (сложное слово)
        var progress = new LearningProgress
        {
            UserId = _testUserId,
            WordId = word.Id,
            KnowledgeLevel = 0, // Сброшенный уровень
            NextReview = DateTime.UtcNow.AddDays(-1),
            LastPracticed = DateTime.UtcNow.AddDays(-1),
            TotalAttempts = 10,
            CorrectAnswers = 3 // Низкий процент успеха
        };
        _context.LearningProgresses.Add(progress);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetDailyPlan();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var plan = okResult.Value.Should().BeOfType<DailyPlanDto>().Subject;
        
        plan.DifficultWords.Should().HaveCount(1);
        plan.Stats.TotalDifficultCount.Should().Be(1);
    }

    [Fact]
    public async Task GetDailyPlan_RespectsLimits()
    {
        // Arrange
        var dictionary = await CreateTestDictionary();
        
        // Создаём 15 новых слов
        for (int i = 0; i < 15; i++)
        {
            await CreateTestWord(dictionary.Id, $"word{i}", $"слово{i}");
        }

        // Act - запрашиваем только 5 новых слов
        var result = await _controller.GetDailyPlan(newWordsLimit: 5);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var plan = okResult.Value.Should().BeOfType<DailyPlanDto>().Subject;
        
        plan.NewWords.Should().HaveCount(5);
        plan.Stats.TotalNewCount.Should().Be(15); // Общее количество остаётся 15
    }

    #endregion

    #region GetTrainingWords Tests

    [Fact]
    public async Task GetTrainingWords_MixedMode_ReturnsMixedWords()
    {
        // Arrange
        var dictionary = await CreateTestDictionary();
        
        // Новые слова
        var newWord = await CreateTestWord(dictionary.Id, "new", "новый");
        
        // Слова к повторению
        var reviewWord = await CreateTestWord(dictionary.Id, "review", "повторение");
        await CreateTestProgress(reviewWord.Id, knowledgeLevel: 2, nextReview: DateTime.UtcNow.AddDays(-1));

        // Act
        var result = await _controller.GetTrainingWords(mode: "mixed", limit: 10);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var words = okResult.Value.Should().BeAssignableTo<List<TrainingWordDto>>().Subject;
        
        words.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public async Task GetTrainingWords_ReviewMode_ReturnsOnlyReviewWords()
    {
        // Arrange
        var dictionary = await CreateTestDictionary();
        
        var newWord = await CreateTestWord(dictionary.Id, "new", "новый");
        var reviewWord = await CreateTestWord(dictionary.Id, "review", "повторение");
        await CreateTestProgress(reviewWord.Id, knowledgeLevel: 2, nextReview: DateTime.UtcNow.AddDays(-1));

        // Act
        var result = await _controller.GetTrainingWords(mode: "review", limit: 10);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var words = okResult.Value.Should().BeAssignableTo<List<TrainingWordDto>>().Subject;
        
        words.Should().HaveCount(1);
        words[0].OriginalWord.Should().Be("review");
    }

    [Fact]
    public async Task GetTrainingWords_NewMode_ReturnsOnlyNewWords()
    {
        // Arrange
        var dictionary = await CreateTestDictionary();
        
        var newWord = await CreateTestWord(dictionary.Id, "new", "новый");
        var reviewWord = await CreateTestWord(dictionary.Id, "review", "повторение");
        await CreateTestProgress(reviewWord.Id, knowledgeLevel: 2, nextReview: DateTime.UtcNow.AddDays(-1));

        // Act
        var result = await _controller.GetTrainingWords(mode: "new", limit: 10);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var words = okResult.Value.Should().BeAssignableTo<List<TrainingWordDto>>().Subject;
        
        words.Should().HaveCount(1);
        words[0].OriginalWord.Should().Be("new");
    }

    [Fact]
    public async Task GetTrainingWords_FiltersByDictionary()
    {
        // Arrange
        var dictionary1 = await CreateTestDictionary("Dict1");
        var dictionary2 = await CreateTestDictionary("Dict2");
        
        await CreateTestWord(dictionary1.Id, "word1", "слово1");
        await CreateTestWord(dictionary2.Id, "word2", "слово2");

        // Act
        var result = await _controller.GetTrainingWords(mode: "new", dictionaryId: dictionary1.Id);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var words = okResult.Value.Should().BeAssignableTo<List<TrainingWordDto>>().Subject;
        
        words.Should().HaveCount(1);
        words[0].OriginalWord.Should().Be("word1");
    }

    #endregion

    #region StarterPack Tests

    [Fact]
    public async Task GetStarterPack_ForNewUser_CreatesStarterDictionary()
    {
        // Act
        var result = await _controller.GetStarterPack();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        
        // Проверяем, что словарь создан
        var dictionaries = _context.Dictionaries.Where(d => d.UserId == _testUserId).ToList();
        dictionaries.Should().HaveCount(1);
        dictionaries[0].Name.Should().Be("Базовые слова");
        
        // Проверяем, что слова добавлены
        var words = _context.Words.Where(w => w.UserId == _testUserId).ToList();
        words.Should().HaveCount(20);
    }

    [Fact]
    public async Task GetStarterPack_ForUserWithContent_ReturnsBadRequest()
    {
        // Arrange
        await CreateTestDictionary();

        // Act
        var result = await _controller.GetStarterPack();

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    #endregion
}
