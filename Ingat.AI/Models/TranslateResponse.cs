namespace Ingat.AI.Models;

public sealed class TranslateResponse
{
    public string Translation { get; set; } = string.Empty;
    public List<string> Alternatives { get; set; } = new();
}
