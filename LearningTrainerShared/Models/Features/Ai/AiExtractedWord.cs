namespace LearningTrainerShared.Models.Features.Ai;

/// <summary>
/// Слово, извлечённое ИИ из текста пользователя.
/// </summary>
public record AiExtractedWord(string Original, string Translation, string? PartOfSpeech, string? Context);
