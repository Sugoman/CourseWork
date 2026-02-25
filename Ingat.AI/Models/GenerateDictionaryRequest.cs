namespace Ingat.AI.Models;

public sealed class GenerateDictionaryRequest
{
    public string Topic { get; set; } = string.Empty;
    public string SourceLanguage { get; set; } = "English";
    public string TargetLanguage { get; set; } = "Russian";
    /// <summary>CEFR level: A1, A2, B1, B2, C1, C2.</summary>
    public string LanguageLevel { get; set; } = "A2";
    /// <summary>How many words to generate (clamped 5–30).</summary>
    public int WordCount { get; set; } = 10;
}
