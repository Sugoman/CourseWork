namespace LearningTrainerShared.Models.Features.Ai;

/// <summary>
/// Результат автоопределения языка текста.
/// </summary>
public record AiDetectedLanguage(string Language, double Confidence);
