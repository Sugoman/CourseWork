using FluentAssertions;
using LearningAPI.Tests.Helpers;
using LearningTrainerShared.Models;
using Xunit;

namespace LearningAPI.Tests.Models;

public class EntityTests
{
    #region Dictionary Tests

    [Fact]
    public void Dictionary_WordCount_ReturnsCorrectCount()
    {
        // Arrange
        var dictionary = new Dictionary
        {
            Id = 1,
            Name = "Test",
            UserId = 1,
            Words = new List<Word>
            {
                new Word { Id = 1, OriginalWord = "Hello", Translation = "Привет" },
                new Word { Id = 2, OriginalWord = "World", Translation = "Мир" },
                new Word { Id = 3, OriginalWord = "Test", Translation = "Тест" }
            }
        };

        // Act & Assert
        dictionary.WordCount.Should().Be(3);
    }

    [Fact]
    public void Dictionary_WordCount_WithEmptyWords_ReturnsZero()
    {
        // Arrange
        var dictionary = new Dictionary
        {
            Id = 1,
            Name = "Test",
            UserId = 1,
            Words = new List<Word>()
        };

        // Act & Assert
        dictionary.WordCount.Should().Be(0);
    }

    [Fact]
    public void Dictionary_WordCount_WithNullWords_ReturnsZero()
    {
        // Arrange
        var dictionary = new Dictionary
        {
            Id = 1,
            Name = "Test",
            UserId = 1,
            Words = null!
        };

        // Act & Assert
        dictionary.WordCount.Should().Be(0);
    }

    [Fact]
    public void Dictionary_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var dictionary = new Dictionary();

        // Assert
        dictionary.IsPublished.Should().BeFalse();
        dictionary.Rating.Should().Be(0);
        dictionary.RatingCount.Should().Be(0);
        dictionary.DownloadCount.Should().Be(0);
        dictionary.SourceDictionaryId.Should().BeNull();
    }

    #endregion

    #region Rule Tests

    [Fact]
    public void Rule_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var rule = new Rule();

        // Assert
        rule.IsPublished.Should().BeFalse();
        rule.Rating.Should().Be(0);
        rule.RatingCount.Should().Be(0);
        rule.DownloadCount.Should().Be(0);
        rule.SourceRuleId.Should().BeNull();
        rule.DifficultyLevel.Should().Be(1);
        rule.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    #endregion

    #region LearningProgress Tests

    [Fact]
    public void LearningProgress_SuccessRate_CalculatesCorrectly()
    {
        // Arrange
        var progress = new LearningProgress
        {
            TotalAttempts = 10,
            CorrectAnswers = 8
        };

        // Act & Assert
        progress.SuccessRate.Should().Be(0.8);
    }

    [Fact]
    public void LearningProgress_SuccessRate_WithZeroAttempts_ReturnsZero()
    {
        // Arrange
        var progress = new LearningProgress
        {
            TotalAttempts = 0,
            CorrectAnswers = 0
        };

        // Act & Assert
        progress.SuccessRate.Should().Be(0);
    }

    [Fact]
    public void LearningProgress_SuccessRate_With100Percent_ReturnsOne()
    {
        // Arrange
        var progress = new LearningProgress
        {
            TotalAttempts = 5,
            CorrectAnswers = 5
        };

        // Act & Assert
        progress.SuccessRate.Should().Be(1.0);
    }

    #endregion

    #region Word Tests

    [Fact]
    public void Word_AddedAt_DefaultsToUtcNow()
    {
        // Arrange & Act
        var word = new Word();

        // Assert
        word.AddedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    #endregion

    #region User Tests

    [Fact]
    public void User_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var user = new User();

        // Assert
        user.IsRefreshTokenRevoked.Should().BeFalse();
        user.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    #endregion
}
