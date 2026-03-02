namespace LearningTrainerShared.Models;

/// <summary>
/// План тренировки на сегодня
/// </summary>
public class DailyPlanDto
{
    /// <summary>
    /// Слова, которые нужно повторить сегодня
    /// </summary>
    public List<TrainingWordDto> ReviewWords { get; set; } = new();
    
    /// <summary>
    /// Новые слова для изучения
    /// </summary>
    public List<TrainingWordDto> NewWords { get; set; } = new();
    
    /// <summary>
    /// Слова с ошибками (сложные)
    /// </summary>
    public List<TrainingWordDto> DifficultWords { get; set; } = new();
    
    /// <summary>
    /// Общая статистика плана
    /// </summary>
    public DailyPlanStats Stats { get; set; } = new();
}

/// <summary>
/// Статистика плана на день
/// </summary>
public class DailyPlanStats
{
    public int TotalReviewCount { get; set; }
    public int TotalNewCount { get; set; }
    public int TotalDifficultCount { get; set; }
    public int CompletedToday { get; set; }
    public int CurrentStreak { get; set; }
    public DateTime? LastPracticeDate { get; set; }

    /// <summary>
    /// Количество слов-пиявок (leech), заблокированных из обычной очереди.
    /// </summary>
    public int LeechCount { get; set; }

    /// <summary>
    /// Дневная цель пользователя (количество слов). 0 = не задана.
    /// </summary>
    public int DailyGoal { get; set; }
}

/// <summary>
/// Слово для тренировки
/// </summary>
public class TrainingWordDto
{
    public int WordId { get; set; }
    public string OriginalWord { get; set; } = string.Empty;
    public string Translation { get; set; } = string.Empty;
    public string? Transcription { get; set; }
    public string? Example { get; set; }
    public string DictionaryName { get; set; } = string.Empty;
    public int DictionaryId { get; set; }
    public string? DictionaryTags { get; set; }
    public int KnowledgeLevel { get; set; }
    public DateTime? NextReview { get; set; }
    public int TotalAttempts { get; set; }
    public int CorrectAnswers { get; set; }

    /// <summary>
    /// Количество «сбросов» (leech indicator).
    /// </summary>
    public int LapseCount { get; set; }

    /// <summary>
    /// Слово помечено как leech и заморожено.
    /// </summary>
    public bool IsLeech { get; set; }

    /// <summary>
    /// Процент правильных ответов (0–100). Если попыток не было — null.
    /// Используется для визуализации прогресса слова в тренировке (#10 LEARNING_IMPROVEMENTS).
    /// </summary>
    public double? SuccessRate => TotalAttempts > 0
        ? Math.Round((double)CorrectAnswers / TotalAttempts * 100, 1)
        : null;

    /// <summary>
    /// Уровень мастерства для отображения (●●●○○). Максимум 5.
    /// </summary>
    public int MasteryStars => Math.Min(KnowledgeLevel, 5);
}

/// <summary>
/// Результат сессии тренировки
/// </summary>
public class TrainingSessionResultDto
{
    public int TotalWords { get; set; }
    public int CorrectAnswers { get; set; }
    public int WrongAnswers { get; set; }
    public TimeSpan Duration { get; set; }
    public double AccuracyPercent => TotalWords > 0 ? (double)CorrectAnswers / TotalWords * 100 : 0;
    public List<WordResultDto> WordResults { get; set; } = new();
}

/// <summary>
/// Результат ответа на одно слово
/// </summary>
public class WordResultDto
{
    public int WordId { get; set; }
    public string OriginalWord { get; set; } = string.Empty;
    public ResponseQuality Quality { get; set; }
    public bool WasCorrect => Quality >= ResponseQuality.Good;
}
