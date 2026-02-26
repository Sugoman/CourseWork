namespace LearningTrainerShared.Models.Features.Ai;

/// <summary>
/// Результат генерации грамматического упражнения через ИИ.
/// </summary>
public record AiExerciseResult(
    string Question,
    List<string> Options,
    int CorrectIndex,
    string Explanation);
