namespace Ingat.AI.Models;

public sealed class ExtractWordsResponse
{
    public List<ExtractedWord> Words { get; set; } = new();
}

public sealed class ExtractedWord
{
    public string Original { get; set; } = string.Empty;
    public string Translation { get; set; } = string.Empty;
    public string? PartOfSpeech { get; set; }
    public string? Context { get; set; }
}
