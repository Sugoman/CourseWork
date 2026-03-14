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

    // Chain support
    public string? ChainId { get; set; }
    public int ChainOrder { get; set; }
    public bool IsSecret { get; set; }
    public string? SecretHint { get; set; }
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
    int TargetValue,
    string? ChainId = null,
    int ChainOrder = 0,
    bool IsSecret = false,
    string? SecretHint = null
);

/// <summary>
/// Метаданные цепочки достижений
/// </summary>
public record AchievementChain(string Id, string Name, string Icon, string Description);

/// <summary>
/// Все достижения системы
/// </summary>
public static class AchievementDefinitions
{
    /// <summary>
    /// Определения цепочек
    /// </summary>
    public static readonly List<AchievementChain> Chains = new()
    {
        new("learning", "Путь знаний", "📚", "Выучите как можно больше слов"),
        new("consistency", "Несокрушимость", "🔥", "Поддерживайте серию ежедневных занятий"),
        new("accuracy", "Точность", "🎯", "Достигайте высокой точности ответов"),
        new("speed", "Скорость", "⚡", "Проходите тренировки быстрее"),
        new("explorer", "Исследователь", "🗺️", "Создавайте и осваивайте словари"),
        new("social", "Сообщество", "🤝", "Делитесь контентом и помогайте другим"),
    };

    public static readonly List<AchievementDefinition> All = new()
    {
        // === LEARNING CHAIN ===
        new("first_word", "Первое слово", "Выучите первое слово", "🎯", AchievementCategory.Learning, AchievementRarity.Common, 1, "learning", 1),
        new("words_10", "Начинающий", "Выучите 10 слов", "📚", AchievementCategory.Learning, AchievementRarity.Common, 10, "learning", 2),
        new("words_50", "Ученик", "Выучите 50 слов", "📖", AchievementCategory.Learning, AchievementRarity.Uncommon, 50, "learning", 3),
        new("words_100", "Знаток", "Выучите 100 слов", "🎓", AchievementCategory.Learning, AchievementRarity.Uncommon, 100, "learning", 4),
        new("words_500", "Эксперт", "Выучите 500 слов", "🏆", AchievementCategory.Learning, AchievementRarity.Rare, 500, "learning", 5),
        new("words_1000", "Мастер", "Выучите 1000 слов", "👑", AchievementCategory.Learning, AchievementRarity.Epic, 1000, "learning", 6),
        new("words_5000", "Полиглот", "Выучите 5000 слов", "🌟", AchievementCategory.Learning, AchievementRarity.Legendary, 5000, "learning", 7),

        // === CONSISTENCY CHAIN ===
        new("streak_3", "Тройка", "3 дня подряд", "🔥", AchievementCategory.Consistency, AchievementRarity.Common, 3, "consistency", 1),
        new("streak_7", "Неделя силы", "7 дней подряд", "🔥", AchievementCategory.Consistency, AchievementRarity.Uncommon, 7, "consistency", 2),
        new("streak_30", "Месяц упорства", "30 дней подряд", "🔥", AchievementCategory.Consistency, AchievementRarity.Rare, 30, "consistency", 3),
        new("streak_100", "Железная воля", "100 дней подряд", "💪", AchievementCategory.Consistency, AchievementRarity.Epic, 100, "consistency", 4),
        new("streak_365", "Год мастерства", "365 дней подряд", "🏅", AchievementCategory.Consistency, AchievementRarity.Legendary, 365, "consistency", 5),

        // === ACCURACY CHAIN ===
        new("perfect_session", "Без ошибок", "Завершите сессию без единой ошибки (мин. 10 слов)", "✨", AchievementCategory.Accuracy, AchievementRarity.Uncommon, 1, "accuracy", 1),
        new("accuracy_90", "Точный стрелок", "Достигните 90% общей точности", "🎯", AchievementCategory.Accuracy, AchievementRarity.Rare, 90, "accuracy", 2),
        new("accuracy_95", "Снайпер", "Достигните 95% общей точности", "💯", AchievementCategory.Accuracy, AchievementRarity.Epic, 95, "accuracy", 3),

        // === SPEED CHAIN ===
        new("speed_demon", "Скоростной", "50 слов за 10 минут", "⚡", AchievementCategory.Speed, AchievementRarity.Rare, 50, "speed", 1),
        new("marathon", "Марафонец", "100 слов за одну сессию", "🏃", AchievementCategory.Speed, AchievementRarity.Rare, 100, "speed", 2),

        // === EXPLORER CHAIN ===
        new("first_dict", "Коллекционер", "Создайте первый словарь", "📕", AchievementCategory.Explorer, AchievementRarity.Common, 1, "explorer", 1),
        new("dict_5", "Библиотекарь", "Создайте 5 словарей", "📚", AchievementCategory.Explorer, AchievementRarity.Uncommon, 5, "explorer", 2),
        new("all_levels", "Максимальный уровень", "Достигните 5 уровня по всем словам словаря", "⭐", AchievementCategory.Explorer, AchievementRarity.Epic, 1, "explorer", 3),

        // === SOCIAL CHAIN ===
        new("share_first", "Делиться - значит заботиться", "Поделитесь словарём или правилом", "🤝", AchievementCategory.Social, AchievementRarity.Uncommon, 1, "social", 1),
        new("popular", "Популярный", "100 скачиваний вашего контента", "🌟", AchievementCategory.Social, AchievementRarity.Rare, 100, "social", 2),

        // === SECRET ACHIEVEMENTS ===
        new("night_owl", "Ночная сова", "Завершите тренировку после 23:00", "🦉", AchievementCategory.Consistency, AchievementRarity.Rare, 1,
            IsSecret: true, SecretHint: "Кто-то учится, когда город спит..."),
        new("early_bird", "Ранняя пташка", "Завершите тренировку до 06:00", "🐦", AchievementCategory.Consistency, AchievementRarity.Rare, 1,
            IsSecret: true, SecretHint: "Кто рано встаёт — тому бог подаёт"),
        new("perfectionist", "Перфекционист", "10 безупречных сессий подряд (мин. 10 слов)", "💎", AchievementCategory.Accuracy, AchievementRarity.Legendary, 10,
            IsSecret: true, SecretHint: "Ошибки? Не слышали."),
        new("comeback_kid", "Возвращение", "Вернитесь к обучению после 30+ дней перерыва", "🔄", AchievementCategory.Consistency, AchievementRarity.Epic, 1,
            IsSecret: true, SecretHint: "Иногда нужно уйти, чтобы вернуться"),
        new("speed_king", "Молниеносный", "100 слов за 10 минут", "👑", AchievementCategory.Speed, AchievementRarity.Legendary, 100,
            IsSecret: true, SecretHint: "Быстрее ветра, точнее часов"),
    };

    public static AchievementDefinition? GetById(string id) => All.FirstOrDefault(a => a.Id == id);

    public static AchievementChain? GetChain(string chainId) => Chains.FirstOrDefault(c => c.Id == chainId);

    public static List<AchievementDefinition> GetByChain(string chainId) =>
        All.Where(a => a.ChainId == chainId).OrderBy(a => a.ChainOrder).ToList();
}
