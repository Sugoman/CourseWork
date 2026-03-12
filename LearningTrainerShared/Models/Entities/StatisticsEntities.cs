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

    /// <summary>
    /// Количество доступных Streak Freeze (§19.9 LEARNING_IMPROVEMENTS).
    /// </summary>
    public int StreakFreezeCount { get; set; }

    /// <summary>
    /// Дата последнего использования Streak Freeze.
    /// </summary>
    public DateTime? LastFreezeUsedDate { get; set; }

    public long TotalLearningTimeSeconds { get; set; }
    public int TotalSessions { get; set; }

    /// <summary>
    /// Дневная цель пользователя (количество слов). По умолчанию 20.
    /// </summary>
    public int DailyGoal { get; set; } = 20;

    /// <summary>
    /// Общее количество очков опыта (§5.1 LEARNING_IMPROVEMENTS).
    /// </summary>
    public long TotalXp { get; set; }

    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Вычисляемый уровень пользователя по формуле: level = floor(sqrt(TotalXp / 50)) + 1.
    /// Уровень 1 = 0 XP, Уровень 2 = 50 XP, Уровень 3 = 200 XP, Уровень 5 = 800 XP...
    /// </summary>
    [NotMapped]
    public int Level => (int)Math.Floor(Math.Sqrt(TotalXp / 50.0)) + 1;

    /// <summary>
    /// XP, необходимые для следующего уровня.
    /// </summary>
    [NotMapped]
    public long XpForNextLevel => (long)(Level * Level) * 50;

    /// <summary>
    /// XP, необходимые для текущего уровня (нижняя граница).
    /// </summary>
    [NotMapped]
    public long XpForCurrentLevel => (long)((Level - 1) * (Level - 1)) * 50;
}
