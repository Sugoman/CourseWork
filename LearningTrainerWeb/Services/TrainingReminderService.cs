namespace LearningTrainerWeb.Services;

/// <summary>
/// Настройки напоминаний о тренировке (#7 LEARNING_IMPROVEMENTS)
/// </summary>
public class TrainingReminderSettings
{
    public bool Enabled { get; set; } = true;
    public int IntervalMinutes { get; set; } = 30;
    public int QuietHoursStart { get; set; } = 22; // 22:00
    public int QuietHoursEnd { get; set; } = 8;   // 08:00
}

/// <summary>
/// Сервис напоминаний о тренировке для Blazor Server (#7 LEARNING_IMPROVEMENTS).
/// Использует browser Notification API через JS interop для уведомлений.
/// Хранит настройки в localStorage через JS.
/// </summary>
public interface ITrainingReminderService
{
    /// <summary>
    /// Получить сообщение-напоминание на основе текущего прогресса.
    /// Возвращает null, если напоминание не нужно (тихие часы и т.д.)
    /// </summary>
    Task<ReminderMessage?> GetReminderAsync(int wordsToReview, int currentStreak, int dailyGoal, int completedToday);
}

public class TrainingReminderService : ITrainingReminderService
{
    private static readonly string[] StreakMessages =
    [
        "Не потеряйте серию! 🔥 {0} дней подряд",
        "Ваша серия: {0} дней. Продолжайте! 🔥",
        "Отлично! {0} дней без перерыва 🔥 Не останавливайтесь!"
    ];

    private static readonly string[] ReviewMessages =
    [
        "У вас {0} слов ожидают повторения 📚",
        "{0} слов готовы к повторению! Не откладывайте 📖",
        "Пора повторить! {0} слов ждут вас 🧠"
    ];

    private static readonly string[] GoalMessages =
    [
        "Осталось {0} слов до дневной цели 🎯",
        "Ещё {0} слов — и цель на сегодня выполнена! 💪",
        "До цели всего {0} слов. Вы справитесь! 🏆"
    ];

    private static readonly string[] MotivationMessages =
    [
        "Регулярность — ключ к успеху! Начните тренировку 🚀",
        "Каждый день приближает вас к цели 🌟",
        "5 минут тренировки лучше, чем ничего! ⏱️"
    ];

    private readonly Random _random = new();

    public Task<ReminderMessage?> GetReminderAsync(int wordsToReview, int currentStreak, int dailyGoal, int completedToday)
    {
        var hour = DateTime.Now.Hour;

        // Quiet hours: 22:00 – 08:00 by default
        if (hour >= 22 || hour < 8)
            return Task.FromResult<ReminderMessage?>(null);

        // Already trained enough today
        if (completedToday >= dailyGoal && wordsToReview == 0)
            return Task.FromResult<ReminderMessage?>(null);

        string message;
        string icon;

        if (currentStreak > 0 && completedToday == 0)
        {
            // Streak at risk
            var template = StreakMessages[_random.Next(StreakMessages.Length)];
            message = string.Format(template, currentStreak);
            icon = "🔥";
        }
        else if (wordsToReview > 0)
        {
            // Words to review
            var template = ReviewMessages[_random.Next(ReviewMessages.Length)];
            message = string.Format(template, wordsToReview);
            icon = "📚";
        }
        else if (completedToday < dailyGoal)
        {
            // Goal not met
            var remaining = dailyGoal - completedToday;
            var template = GoalMessages[_random.Next(GoalMessages.Length)];
            message = string.Format(template, remaining);
            icon = "🎯";
        }
        else
        {
            // Generic motivation
            message = MotivationMessages[_random.Next(MotivationMessages.Length)];
            icon = "🌟";
        }

        return Task.FromResult<ReminderMessage?>(new ReminderMessage
        {
            Text = message,
            Icon = icon,
            Title = "Ingat — Напоминание"
        });
    }
}

public class ReminderMessage
{
    public string Title { get; set; } = "";
    public string Text { get; set; } = "";
    public string Icon { get; set; } = "📚";
}
