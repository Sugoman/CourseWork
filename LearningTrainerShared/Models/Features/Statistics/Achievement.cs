namespace LearningTrainerShared.Models.Statistics;

/// <summary>
/// Достижение пользователя
/// </summary>
public class Achievement
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public AchievementCategory Category { get; set; }
    public AchievementRarity Rarity { get; set; }
    public bool IsUnlocked { get; set; }
    public DateTime? UnlockedAt { get; set; }
    public double Progress { get; set; }
    public int? CurrentValue { get; set; }
    public int? TargetValue { get; set; }
}

public enum AchievementCategory
{
    Learning,
    Consistency,
    Accuracy,
    Speed,
    Social,
    Explorer
}

public enum AchievementRarity
{
    Common,
    Uncommon,
    Rare,
    Epic,
    Legendary
}

/// <summary>
/// Определение достижения (константы)
/// </summary>
public record AchievementDefinition(
    string Id,
    string Title,
    string Description,
    string Icon,
    AchievementCategory Category,
    AchievementRarity Rarity,
    int TargetValue
);

/// <summary>
/// Все достижения системы
/// </summary>
public static class AchievementDefinitions
{
    public static readonly List<AchievementDefinition> All = new()
    {
        // === LEARNING ===
        new("first_word", "Первое слово", "Выучите первое слово", "🎯", AchievementCategory.Learning, AchievementRarity.Common, 1),
        new("words_10", "Начинающий", "Выучите 10 слов", "📚", AchievementCategory.Learning, AchievementRarity.Common, 10),
        new("words_50", "Ученик", "Выучите 50 слов", "📖", AchievementCategory.Learning, AchievementRarity.Uncommon, 50),
        new("words_100", "Знаток", "Выучите 100 слов", "🎓", AchievementCategory.Learning, AchievementRarity.Uncommon, 100),
        new("words_500", "Эксперт", "Выучите 500 слов", "🏆", AchievementCategory.Learning, AchievementRarity.Rare, 500),
        new("words_1000", "Мастер", "Выучите 1000 слов", "👑", AchievementCategory.Learning, AchievementRarity.Epic, 1000),
        new("words_5000", "Полиглот", "Выучите 5000 слов", "🌟", AchievementCategory.Learning, AchievementRarity.Legendary, 5000),

        // === CONSISTENCY ===
        new("streak_3", "Тройка", "3 дня подряд", "🔥", AchievementCategory.Consistency, AchievementRarity.Common, 3),
        new("streak_7", "Неделя силы", "7 дней подряд", "🔥", AchievementCategory.Consistency, AchievementRarity.Uncommon, 7),
        new("streak_30", "Месяц упорства", "30 дней подряд", "🔥", AchievementCategory.Consistency, AchievementRarity.Rare, 30),
        new("streak_100", "Железная воля", "100 дней подряд", "💪", AchievementCategory.Consistency, AchievementRarity.Epic, 100),
        new("streak_365", "Год мастерства", "365 дней подряд", "🏅", AchievementCategory.Consistency, AchievementRarity.Legendary, 365),

        // === ACCURACY ===
        new("perfect_session", "Без ошибок", "Завершите сессию без единой ошибки (мин. 10 слов)", "✨", AchievementCategory.Accuracy, AchievementRarity.Uncommon, 1),
        new("accuracy_90", "Точный стрелок", "Достигните 90% общей точности", "🎯", AchievementCategory.Accuracy, AchievementRarity.Rare, 90),
        new("accuracy_95", "Снайпер", "Достигните 95% общей точности", "💯", AchievementCategory.Accuracy, AchievementRarity.Epic, 95),

        // === SPEED ===
        new("speed_demon", "Скоростной", "50 слов за 10 минут", "⚡", AchievementCategory.Speed, AchievementRarity.Rare, 50),
        new("marathon", "Марафонец", "100 слов за одну сессию", "🏃", AchievementCategory.Speed, AchievementRarity.Rare, 100),

        // === EXPLORER ===
        new("first_dict", "Коллекционер", "Создайте первый словарь", "📕", AchievementCategory.Explorer, AchievementRarity.Common, 1),
        new("dict_5", "Библиотекарь", "Создайте 5 словарей", "📚", AchievementCategory.Explorer, AchievementRarity.Uncommon, 5),
        new("all_levels", "Максимальный уровень", "Достигните 5 уровня по всем словам словаря", "⭐", AchievementCategory.Explorer, AchievementRarity.Epic, 1),

        // === SOCIAL ===
        new("share_first", "Делиться - значит заботиться", "Поделитесь словарём или правилом", "🤝", AchievementCategory.Social, AchievementRarity.Uncommon, 1),
        new("popular", "Популярный", "100 скачиваний вашего контента", "🌟", AchievementCategory.Social, AchievementRarity.Rare, 100),
    };

    public static AchievementDefinition? GetById(string id) => All.FirstOrDefault(a => a.Id == id);
}
