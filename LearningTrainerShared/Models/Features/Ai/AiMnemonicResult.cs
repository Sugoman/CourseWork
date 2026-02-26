namespace LearningTrainerShared.Models.Features.Ai;

/// <summary>
/// Мнемоника для запоминания слова, сгенерированная ИИ.
/// </summary>
public record AiMnemonicResult(string Mnemonic, string? Etymology, string? Association);
