namespace Ingat.AI.Models;

public sealed class GenerateTypedExercisesRequest
{
    public string RuleTitle { get; set; } = string.Empty;
    public string RuleContent { get; set; } = string.Empty;
    public string Language { get; set; } = "English";
    public string TargetLanguage { get; set; } = "Russian";
    /// <summary>
    /// Тип упражнения: "mcq", "transformation", "error_correction", "word_order", "translation", "matching"
    /// </summary>
    public string ExerciseType { get; set; } = "mcq";
    public int Count { get; set; } = 3;
    /// <summary>Уровень сложности: 1 = базовый, 2 = средний, 3 = продвинутый</summary>
    public int DifficultyTier { get; set; } = 1;
}

public sealed class GenerateTypedExercisesResponse
{
    public List<GeneratedTypedExercise> Exercises { get; set; } = new();
}

public sealed class GeneratedTypedExercise
{
    public string Question { get; set; } = string.Empty;
    public List<string>? Options { get; set; }
    public int? CorrectIndex { get; set; }
    public string? CorrectAnswer { get; set; }
    public List<string>? AlternativeAnswers { get; set; }
    public string? IncorrectSentence { get; set; }
    public List<string>? ShuffledWords { get; set; }
    public string Explanation { get; set; } = string.Empty;
    public int DifficultyTier { get; set; } = 1;
}

public sealed class ExerciseValidationItem
{
    public int Index { get; set; }
    public bool Valid { get; set; }
    public string Reason { get; set; } = string.Empty;
}
