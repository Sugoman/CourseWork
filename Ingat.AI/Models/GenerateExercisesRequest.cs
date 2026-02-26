namespace Ingat.AI.Models;

public sealed class GenerateExercisesRequest
{
    public string RuleTitle { get; set; } = string.Empty;
    public string RuleContent { get; set; } = string.Empty;
    public string Language { get; set; } = "English";
    public string TargetLanguage { get; set; } = "Russian";
    public int Count { get; set; } = 5;
    /// <summary>CEFR level: A1, A2, B1, B2, C1, C2</summary>
    public string? LanguageLevel { get; set; }
}
