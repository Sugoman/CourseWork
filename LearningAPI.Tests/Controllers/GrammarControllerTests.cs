using FluentAssertions;
using LearningAPI.Controllers;
using LearningAPI.Tests.Helpers;
using LearningTrainerShared.Context;
using LearningTrainerShared.Models;
using LearningTrainerShared.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using System.Security.Claims;
using Xunit;
using Moq;

namespace LearningAPI.Tests.Controllers;

public class GrammarControllerTests : IDisposable
{
    private readonly ApiDbContext _context;
    private readonly GrammarController _controller;
    private readonly int _testUserId = 1;

    public GrammarControllerTests()
    {
        _context = TestDbContextFactory.CreateInMemoryContext();
        var srs = new SpacedRepetitionService();
        var cacheMock = new Mock<IDistributedCache>();

        cacheMock.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        _controller = new GrammarController(_context, srs, cacheMock.Object);
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

    private async Task<Rule> SeedRuleWithExercises(int userId = 0, int exerciseCount = 3, int skillTreeLevel = 1)
    {
        if (userId == 0) userId = _testUserId;

        var role = TestDataSeeder.CreateTeacherRole();
        if (!_context.Roles.Any(r => r.Id == role.Id))
        {
            _context.Roles.Add(role);
        }

        if (!_context.Users.Any(u => u.Id == userId))
        {
            _context.Users.Add(new User
            {
                Id = userId,
                Login = $"user{userId}",
                PasswordHash = "hash",
                Role = role
            });
        }

        var rule = new Rule
        {
            Title = "Present Simple",
            Description = "Basic tense",
            MarkdownContent = "# Present Simple\nUse for habits.",
            Category = "Grammar",
            UserId = userId,
            SkillTreeLevel = skillTreeLevel,
            DifficultyLevel = 1,
            IconEmoji = "📗",
            SkillSummary = "Daily routines",
            XpReward = 50,
            Exercises = new List<GrammarExercise>()
        };

        for (int i = 0; i < exerciseCount; i++)
        {
            rule.Exercises.Add(new GrammarExercise
            {
                ExerciseType = "mcq",
                Question = $"She ___ to school every day. (#{i + 1})",
                OptionsJson = "[\"go\", \"goes\", \"going\", \"gone\"]",
                CorrectIndex = 1,
                CorrectAnswer = "goes",
                Explanation = "Present Simple, 3rd person singular → -es",
                DifficultyTier = 1,
                OrderIndex = i
            });
        }

        _context.Rules.Add(rule);
        await _context.SaveChangesAsync();
        return rule;
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    #region GetSkillTree Tests

    [Fact]
    public async Task GetSkillTree_WithUserRules_ReturnsSkillNodes()
    {
        // Arrange
        await SeedRuleWithExercises();

        // Act
        var result = await _controller.GetSkillTree();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task GetSkillTree_WithNoRules_ReturnsEmptyList()
    {
        // Arrange — no rules seeded

        // Act
        var result = await _controller.GetSkillTree();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task GetSkillTree_WithProgress_ReturnsKnowledgeLevel()
    {
        // Arrange
        var rule = await SeedRuleWithExercises();
        _context.GrammarProgresses.Add(new GrammarProgress
        {
            UserId = _testUserId,
            RuleId = rule.Id,
            KnowledgeLevel = 3,
            TotalSessions = 5,
            CorrectAnswers = 40,
            TotalAnswers = 50
        });
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetSkillTree();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    #endregion

    #region GetDueReviews Tests

    [Fact]
    public async Task GetDueReviews_WithDueRules_ReturnsRules()
    {
        // Arrange
        var rule = await SeedRuleWithExercises();
        _context.GrammarProgresses.Add(new GrammarProgress
        {
            UserId = _testUserId,
            RuleId = rule.Id,
            KnowledgeLevel = 2,
            NextReview = DateTime.UtcNow.AddDays(-1), // Overdue
            TotalSessions = 3,
            CorrectAnswers = 20,
            TotalAnswers = 30
        });
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetDueReviews();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task GetDueReviews_WithNoDueRules_ReturnsEmptyList()
    {
        // Arrange
        var rule = await SeedRuleWithExercises();
        _context.GrammarProgresses.Add(new GrammarProgress
        {
            UserId = _testUserId,
            RuleId = rule.Id,
            KnowledgeLevel = 2,
            NextReview = DateTime.UtcNow.AddDays(5), // Not due yet
            TotalSessions = 3,
            CorrectAnswers = 20,
            TotalAnswers = 30
        });
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetDueReviews();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    #endregion

    #region StartPractice Tests

    [Fact]
    public async Task StartPractice_WithValidRule_ReturnsExercises()
    {
        // Arrange
        var rule = await SeedRuleWithExercises(exerciseCount: 5);

        // Act
        var result = await _controller.StartPractice(rule.Id, 3);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task StartPractice_WithNonExistentRule_ReturnsNotFound()
    {
        // Act
        var result = await _controller.StartPractice(999);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task StartPractice_WithOtherUsersRule_ReturnsNotFound()
    {
        // Arrange — create a rule owned by another user
        var otherUserId = 2;
        var rule = await SeedRuleWithExercises(userId: otherUserId);

        // Act
        var result = await _controller.StartPractice(rule.Id);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    #endregion

    #region SubmitSession Tests

    [Fact]
    public async Task SubmitSession_WithValidData_ReturnsResult()
    {
        // Arrange
        var rule = await SeedRuleWithExercises(exerciseCount: 3);

        // Seed UserStats for XP
        _context.UserStats.Add(new UserStats { UserId = _testUserId });
        await _context.SaveChangesAsync();

        var exerciseIds = rule.Exercises.Select(e => e.Id).ToList();
        var session = new GrammarSessionResult
        {
            Answers = exerciseIds.Select(id => new GrammarAnswerItem
            {
                ExerciseId = id,
                UserAnswer = "goes",
                IsCorrect = true,
                ResponseTimeMs = 3000
            }).ToList()
        };

        // Act
        var result = await _controller.SubmitSession(rule.Id, session);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task SubmitSession_WithNonExistentRule_ReturnsNotFound()
    {
        // Arrange
        var session = new GrammarSessionResult
        {
            Answers = new List<GrammarAnswerItem>
            {
                new() { ExerciseId = 1, IsCorrect = true }
            }
        };

        // Act
        var result = await _controller.SubmitSession(999, session);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task SubmitSession_WithEmptyAnswers_ReturnsBadRequest()
    {
        // Arrange
        var rule = await SeedRuleWithExercises();
        var session = new GrammarSessionResult { Answers = new List<GrammarAnswerItem>() };

        // Act
        var result = await _controller.SubmitSession(rule.Id, session);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task SubmitSession_WithHighAccuracy_IncreasesKnowledgeLevel()
    {
        // Arrange
        var rule = await SeedRuleWithExercises(exerciseCount: 5);
        _context.UserStats.Add(new UserStats { UserId = _testUserId });
        await _context.SaveChangesAsync();

        var session = new GrammarSessionResult
        {
            Answers = rule.Exercises.Select(e => new GrammarAnswerItem
            {
                ExerciseId = e.Id,
                IsCorrect = true,
                ResponseTimeMs = 2000
            }).ToList()
        };

        // Act
        await _controller.SubmitSession(rule.Id, session);

        // Assert
        var progress = _context.GrammarProgresses
            .FirstOrDefault(gp => gp.UserId == _testUserId && gp.RuleId == rule.Id);
        progress.Should().NotBeNull();
        progress!.KnowledgeLevel.Should().BeGreaterThan(0);
        progress.TotalSessions.Should().Be(1);
    }

    [Fact]
    public async Task SubmitSession_WithLowAccuracy_DecreasesOrMaintainsLevel()
    {
        // Arrange
        var rule = await SeedRuleWithExercises(exerciseCount: 5);
        _context.UserStats.Add(new UserStats { UserId = _testUserId });

        // Seed existing progress with KnowledgeLevel 2
        _context.GrammarProgresses.Add(new GrammarProgress
        {
            UserId = _testUserId,
            RuleId = rule.Id,
            KnowledgeLevel = 2,
            EaseFactor = 2.5,
            TotalSessions = 3,
            CorrectAnswers = 10,
            TotalAnswers = 15
        });
        await _context.SaveChangesAsync();

        // Only 1 out of 5 correct = 20% accuracy → Again
        var session = new GrammarSessionResult
        {
            Answers = rule.Exercises.Select((e, i) => new GrammarAnswerItem
            {
                ExerciseId = e.Id,
                IsCorrect = i == 0, // only first is correct
                ResponseTimeMs = 5000
            }).ToList()
        };

        // Act
        await _controller.SubmitSession(rule.Id, session);

        // Assert
        var progress = _context.GrammarProgresses
            .FirstOrDefault(gp => gp.UserId == _testUserId && gp.RuleId == rule.Id);
        progress.Should().NotBeNull();
        progress!.KnowledgeLevel.Should().BeLessThan(2); // Should decrease
        progress.LapseCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task SubmitSession_CreatesProgressIfNotExists()
    {
        // Arrange
        var rule = await SeedRuleWithExercises(exerciseCount: 2);
        _context.UserStats.Add(new UserStats { UserId = _testUserId });
        await _context.SaveChangesAsync();

        var session = new GrammarSessionResult
        {
            Answers = rule.Exercises.Select(e => new GrammarAnswerItem
            {
                ExerciseId = e.Id,
                IsCorrect = true,
                ResponseTimeMs = 2000
            }).ToList()
        };

        // Act
        await _controller.SubmitSession(rule.Id, session);

        // Assert
        var progress = _context.GrammarProgresses
            .FirstOrDefault(gp => gp.UserId == _testUserId && gp.RuleId == rule.Id);
        progress.Should().NotBeNull();
        progress!.TotalSessions.Should().Be(1);
        progress.CorrectAnswers.Should().Be(2);
        progress.TotalAnswers.Should().Be(2);
    }

    [Fact]
    public async Task SubmitSession_AwardsXp_WhenAccuracyAbove50Percent()
    {
        // Arrange
        var rule = await SeedRuleWithExercises(exerciseCount: 4);
        _context.UserStats.Add(new UserStats { UserId = _testUserId, TotalXp = 100 });
        await _context.SaveChangesAsync();

        // 3 out of 4 = 75% accuracy
        var session = new GrammarSessionResult
        {
            Answers = rule.Exercises.Select((e, i) => new GrammarAnswerItem
            {
                ExerciseId = e.Id,
                IsCorrect = i < 3,
                ResponseTimeMs = 3000
            }).ToList()
        };

        // Act
        await _controller.SubmitSession(rule.Id, session);

        // Assert
        var stats = _context.UserStats.FirstOrDefault(s => s.UserId == _testUserId);
        stats.Should().NotBeNull();
        stats!.TotalXp.Should().BeGreaterThan(100);
    }

    #endregion

    #region GetProgressSummary Tests

    [Fact]
    public async Task GetProgressSummary_WithProgress_ReturnsSummary()
    {
        // Arrange
        var rule1 = await SeedRuleWithExercises();

        _context.GrammarProgresses.Add(new GrammarProgress
        {
            UserId = _testUserId,
            RuleId = rule1.Id,
            KnowledgeLevel = 4,
            TotalSessions = 10,
            CorrectAnswers = 80,
            TotalAnswers = 100
        });
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetProgressSummary();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task GetProgressSummary_WithNoProgress_ReturnsZeroSummary()
    {
        // Act
        var result = await _controller.GetProgressSummary();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().NotBeNull();
    }

    #endregion
}
