namespace Ingat.AI.Models;

public sealed class ExtractWordsRequest
{
    public string Text { get; set; } = string.Empty;
    public string Language { get; set; } = "English";
    public string TargetLanguage { get; set; } = "Russian";
    public int MaxWords { get; set; } = 20;
    /// <summary>CEFR level: A1, A2, B1, B2, C1, C2. Words above this level are extracted.</summary>
    public string? LanguageLevel { get; set; }
}
