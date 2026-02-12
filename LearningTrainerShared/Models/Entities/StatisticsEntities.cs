using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace LearningTrainerShared.Models;

/// <summary>
/// История сессий тренировок
/// </summary>
public class TrainingSession
{
    [Key]
    public int Id { get; set; }

    public int UserId { get; set; }

    [JsonIgnore]
    public User? User { get; set; }

    public DateTime StartedAt { get; set; }
    public DateTime CompletedAt { get; set; }

    [NotMapped]
    public TimeSpan Duration => CompletedAt - StartedAt;

    public int WordsReviewed { get; set; }
    public int CorrectAnswers { get; set; }
    public int WrongAnswers { get; set; }

    public string Mode { get; set; } = "Flashcards"; // Flashcards, Typing, Mixed

    public int? DictionaryId { get; set; }

    [JsonIgnore]
    public Dictionary? Dictionary { get; set; }
}

/// <summary>
/// Достижения пользователя
/// </summary>
public class UserAchievement
{
    [Key]
    public int Id { get; set; }

    public int UserId { get; set; }

    [JsonIgnore]
    public User? User { get; set; }

    [MaxLength(100)]
    public string AchievementId { get; set; } = string.Empty;

    public DateTime UnlockedAt { get; set; }

    public int? CurrentProgress { get; set; }
}

/// <summary>
/// Агрегированная статистика пользователя
/// </summary>
public class UserStats
{
    [Key]
    public int UserId { get; set; }

    [JsonIgnore]
    public User? User { get; set; }

    public int CurrentStreak { get; set; }
    public int BestStreak { get; set; }
    public DateTime? LastPracticeDate { get; set; }

    public long TotalLearningTimeSeconds { get; set; }
    public int TotalSessions { get; set; }

    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}
