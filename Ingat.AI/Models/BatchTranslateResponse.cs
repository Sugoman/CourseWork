namespace Ingat.AI.Models;

public sealed class BatchTranslateResponse
{
    public List<BatchTranslateItem> Translations { get; set; } = new();
}

public sealed class BatchTranslateItem
{
    public string Word { get; set; } = string.Empty;
    public string Translation { get; set; } = string.Empty;
    public List<string> Alternatives { get; set; } = new();
}
