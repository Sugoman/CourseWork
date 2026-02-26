namespace Ingat.AI.Models;

public sealed class BatchTranslateRequest
{
    public List<string> Words { get; set; } = new();
    public string SourceLanguage { get; set; } = "English";
    public string TargetLanguage { get; set; } = "Russian";
}
