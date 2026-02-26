namespace Ingat.AI.Models;

public sealed class MnemonicResponse
{
    public string Mnemonic { get; set; } = string.Empty;
    public string? Etymology { get; set; }
    public string? Association { get; set; }
}
