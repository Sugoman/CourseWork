namespace LearningTrainerShared.Models.Features.Ai;

/// <summary>
/// Одна запись сгенерированного словаря от ИИ.
/// </summary>
public record AiGeneratedWordEntry(
    string Original,
    string Translation,
    string PartOfSpeech,
    string Example);
