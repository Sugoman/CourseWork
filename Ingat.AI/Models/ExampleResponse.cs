namespace Ingat.AI.Models;

public sealed class ExampleResponse
{
    public List<ExampleItem> Examples { get; set; } = new();
}

public sealed class ExampleItem
{
    public string Sentence { get; set; } = string.Empty;
    public string Translation { get; set; } = string.Empty;
}
