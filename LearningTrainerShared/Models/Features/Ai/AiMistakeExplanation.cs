namespace LearningTrainerShared.Models.Features.Ai;

/// <summary>
/// Объяснение ошибки пользователя через ИИ.
/// </summary>
public record AiMistakeExplanation(string Explanation, string? Tip);
