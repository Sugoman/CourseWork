namespace Ingat.AI.Models;

public sealed class DetectLanguageResponse
{
    public string Language { get; set; } = string.Empty;
    public double Confidence { get; set; }
}
