namespace Ingat.AI.Models;

public sealed class MnemonicRequest
{
    public string Word { get; set; } = string.Empty;
    public string Translation { get; set; } = string.Empty;
    public string Language { get; set; } = "English";
    public string TargetLanguage { get; set; } = "Russian";
}
