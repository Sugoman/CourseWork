namespace LearningTrainerShared.Models.Features.Ai;

/// <summary>
/// Типизированное грамматическое упражнение, сгенерированное ИИ (§17.8 LEARNING_IMPROVEMENTS).
/// </summary>
public class AiTypedExerciseResult
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
