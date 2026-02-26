namespace Ingat.AI.Models;

public sealed class ExplainMistakeResponse
{
    public string Explanation { get; set; } = string.Empty;
    public string? Tip { get; set; }
}
