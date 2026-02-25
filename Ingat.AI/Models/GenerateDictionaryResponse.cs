namespace Ingat.AI.Models;

public sealed class GenerateDictionaryResponse
{
    public List<GeneratedWordItem> Words { get; set; } = new();
}

public sealed class GeneratedWordItem
{
    public string Original { get; set; } = string.Empty;
    public string Translation { get; set; } = string.Empty;
    public string PartOfSpeech { get; set; } = string.Empty;
    public string Example { get; set; } = string.Empty;
}
