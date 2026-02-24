namespace Ingat.AI.Models;

public sealed class TranslateRequest
{
    public string Word { get; set; } = string.Empty;
    public string SourceLanguage { get; set; } = "English";
    public string TargetLanguage { get; set; } = "Russian";
    public string? Context { get; set; }
    /// <summary>Part of speech: noun, verb, adjective, adverb, etc. Helps disambiguate.</summary>
    public string? PartOfSpeech { get; set; }
}
