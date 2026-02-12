namespace LearningTrainerShared.Models.Statistics;

/// <summary>
/// Полная статистика пользователя
/// </summary>
public class UserStatistics
{
    // === ОБЩИЕ ПОКАЗАТЕЛИ ===
    public int TotalWords { get; set; }
    public int LearnedWords { get; set; }
    public int InProgressWords { get; set; }
    public int TotalDictionaries { get; set; }
    public double OverallAccuracy { get; set; }

    // === СЕРИИ (STREAKS) ===
    public int CurrentStreak { get; set; }
    public int BestStreak { get; set; }
    public DateTime? LastPracticeDate { get; set; }

    // === ВРЕМЯ ===
    public TimeSpan TotalLearningTime { get; set; }
    public TimeSpan AverageSessionDuration { get; set; }
    public double AverageSecondsPerWord { get; set; }
    public int TotalSessions { get; set; }

    // === ПРОГРЕСС ===
    public int WordsLearnedToday { get; set; }
    public int WordsLearnedThisWeek { get; set; }
    public int WordsLearnedThisMonth { get; set; }
    public int WordsReviewedToday { get; set; }

    // === ДЕТАЛИЗИРОВАННЫЕ ДАННЫЕ ===
    public List<DailyActivityStats> DailyActivity { get; set; } = new();
    public List<WeeklyActivityStats> WeeklyActivity { get; set; } = new();
    public List<MonthlyProgressStats> MonthlyProgress { get; set; } = new();
    public List<DictionaryStats> DictionaryStatistics { get; set; } = new();
    public List<KnowledgeLevelDistribution> KnowledgeDistribution { get; set; } = new();
    public List<HourlyActivityStats> HourlyActivity { get; set; } = new();
    public List<DifficultWord> DifficultWords { get; set; } = new();
    public List<Achievement> Achievements { get; set; } = new();

    // === РЕЙТИНГ ===
    public LeaderboardPosition? LeaderboardPosition { get; set; }
}

/// <summary>
/// Активность за конкретный день
/// </summary>
public class DailyActivityStats
{
    public DateTime Date { get; set; }
    public int WordsReviewed { get; set; }
    public int WordsLearned { get; set; }
    public int NewWordsStarted { get; set; }
    public int CorrectAnswers { get; set; }
    public int WrongAnswers { get; set; }
    public TimeSpan TimeSpent { get; set; }
    public double Accuracy => (CorrectAnswers + WrongAnswers) > 0
        ? (double)CorrectAnswers / (CorrectAnswers + WrongAnswers) * 100
        : 0;
}

/// <summary>
/// Активность за неделю
/// </summary>
public class WeeklyActivityStats
{
    public int WeekNumber { get; set; }
    public int Year { get; set; }
    public DateTime WeekStart { get; set; }
    public DateTime WeekEnd { get; set; }
    public int TotalWordsReviewed { get; set; }
    public int TotalWordsLearned { get; set; }
    public int DaysActive { get; set; }
    public double AverageAccuracy { get; set; }
    public TimeSpan TotalTimeSpent { get; set; }
}

/// <summary>
/// Прогресс за месяц
/// </summary>
public class MonthlyProgressStats
{
    public int Month { get; set; }
    public int Year { get; set; }
    public string MonthName { get; set; } = string.Empty;
    public int WordsLearned { get; set; }
    public int WordsReviewed { get; set; }
    public int DaysActive { get; set; }
    public int LongestStreak { get; set; }
    public double AverageAccuracy { get; set; }
}

/// <summary>
/// Статистика по словарю
/// </summary>
public class DictionaryStats
{
    public int DictionaryId { get; set; }
    public string DictionaryName { get; set; } = string.Empty;
    public int TotalWords { get; set; }
    public int LearnedWords { get; set; }
    public int InProgressWords { get; set; }
    public int NotStartedWords { get; set; }
    public double CompletionPercent { get; set; }
    public double Accuracy { get; set; }
    public DateTime? LastPracticed { get; set; }
}

/// <summary>
/// Распределение по уровням знаний
/// </summary>
public class KnowledgeLevelDistribution
{
    public int Level { get; set; }
    public string LevelName { get; set; } = string.Empty;
    public int Count { get; set; }
    public double Percentage { get; set; }
    public string Color { get; set; } = string.Empty;
}

/// <summary>
/// Активность по часам
/// </summary>
public class HourlyActivityStats
{
    public int Hour { get; set; }
    public int WordsReviewed { get; set; }
    public double AverageAccuracy { get; set; }
}

/// <summary>
/// Сложное слово
/// </summary>
public class DifficultWord
{
    public int WordId { get; set; }
    public string OriginalWord { get; set; } = string.Empty;
    public string Translation { get; set; } = string.Empty;
    public string DictionaryName { get; set; } = string.Empty;
    public int TotalAttempts { get; set; }
    public int WrongAttempts { get; set; }
    public double ErrorRate { get; set; }
    public int CurrentLevel { get; set; }
}

/// <summary>
/// Позиция в рейтинге
/// </summary>
public class LeaderboardPosition
{
    public int GlobalRank { get; set; }
    public int TotalUsers { get; set; }
    public int WeeklyRank { get; set; }
    public int WeeklyPoints { get; set; }
    public double Percentile { get; set; }
}
