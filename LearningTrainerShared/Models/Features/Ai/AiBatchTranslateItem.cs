namespace LearningTrainerShared.Models.Features.Ai;

/// <summary>
/// Результат batch-перевода одного слова.
/// </summary>
public record AiBatchTranslateItem(string Word, string Translation, List<string> Alternatives);
