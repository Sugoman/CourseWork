namespace Ingat.AI.Models;

public sealed class ExampleRequest
{
    public string Word { get; set; } = string.Empty;
    public string Language { get; set; } = "English";
    public string TargetLanguage { get; set; } = "Russian";
    public int Count { get; set; } = 1;
    /// <summary>Part of speech: noun, verb, adjective, etc.</summary>
    public string? PartOfSpeech { get; set; }
    /// <summary>CEFR level: A1, A2, B1, B2, C1, C2. Controls sentence complexity.</summary>
    public string? LanguageLevel { get; set; }
}
