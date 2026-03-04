namespace LearningTrainerShared.Models;

/// <summary>
/// Ежедневный челлендж (§5.2 LEARNING_IMPROVEMENTS).
/// Генерируется детерминированно из текущей даты → все пользователи получают одинаковый челлендж.
/// </summary>
public class DailyChallengeDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = "🎯";
    public int TargetValue { get; set; }
    public int CurrentValue { get; set; }
    public bool IsCompleted => CurrentValue >= TargetValue;
    public int XpReward { get; set; }
    public double Progress => TargetValue > 0 ? Math.Min(1.0, (double)CurrentValue / TargetValue) * 100 : 0;
}
