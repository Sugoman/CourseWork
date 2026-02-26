namespace Ingat.AI.Models;

public sealed class ExplainMistakeRequest
{
    public string Word { get; set; } = string.Empty;
    public string UserAnswer { get; set; } = string.Empty;
    public string CorrectAnswer { get; set; } = string.Empty;
    public string? Context { get; set; }
    public string Language { get; set; } = "English";
    public string TargetLanguage { get; set; } = "Russian";
}
