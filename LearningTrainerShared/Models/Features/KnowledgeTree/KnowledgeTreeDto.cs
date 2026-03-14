namespace LearningTrainerShared.Models.KnowledgeTreeDto;

/// <summary>
/// Полное состояние Дерева Знаний (§3.8 LEARNING_IMPROVEMENTS)
/// </summary>
public class KnowledgeTreeState
{
    // === ОСНОВНОЕ СОСТОЯНИЕ ===
    public TreeStage CurrentStage { get; set; }
    public string StageName { get; set; } = "";
    public string StageEmoji { get; set; } = "";
    public int TotalWordsContributed { get; set; }
    public long TotalXpContributed { get; set; }

    /// <summary>Tree-specific XP (poured by user, separate from UserStats.TotalXp)</summary>
    public long TreeXp { get; set; }

    /// <summary>Tree level derived from TreeXp</summary>
    public int TreeLevel { get; set; }

    /// <summary>User's available XP that can still be poured</summary>
    public long AvailableUserXp { get; set; }

    // === ПРОГРЕСС ДО СЛЕДУЮЩЕЙ СТАДИИ ===
    public int WordsForNextStage { get; set; }
    public long XpForNextStage { get; set; }
    public double ProgressToNextStage { get; set; } // 0..100

    // === СКИН ===
    public int TreeSkinId { get; set; }
    public string TreeSkinName { get; set; } = "";
    public string AssetPrefix { get; set; } = "";

    // === АКТИВНОСТЬ ===
    public DateTime LastActivityAt { get; set; }
    public bool IsWilting { get; set; }
    public int DaysSinceActivity { get; set; }

    // === ВИЗУАЛИЗАЦИЯ: ВЕТКИ (по тегу) ===
    public List<TreeBranch> Branches { get; set; } = new();

    // === ВИЗУАЛИЗАЦИЯ: ПЛОДЫ (правила грамматики) ===
    public List<TreeFruit> Fruits { get; set; } = new();

    /// <summary>Max branches shown on screen (configurable)</summary>
    public int MaxVisibleBranches { get; set; } = 6;
}

/// <summary>
/// Ветка дерева = тег словарей (Branch)
/// </summary>
public class TreeBranch
{
    public string TagName { get; set; } = "";
    public int TotalWords { get; set; }
    public int LearnedWords { get; set; }
    public double CompletionPercent { get; set; }

    /// <summary>Twigs = dictionaries under this tag</summary>
    public List<TreeTwig> Twigs { get; set; } = new();
}

/// <summary>
/// Ветка-прутик = конкретный словарь (Twig)
/// </summary>
public class TreeTwig
{
    public int DictionaryId { get; set; }
    public string DictionaryName { get; set; } = "";
    public int TotalWords { get; set; }
    public int LearnedWords { get; set; }
    public double CompletionPercent { get; set; }

    /// <summary>Up to 100 leaves (words)</summary>
    public List<TreeLeaf> Leaves { get; set; } = new();
}

/// <summary>
/// Листок = слово в словаре (Leaf). Max 100 per twig.
/// </summary>
public class TreeLeaf
{
    public int WordId { get; set; }
    public string Word { get; set; } = "";
    public bool IsLearned { get; set; }
}

/// <summary>
/// Плод дерева = грамматическое правило (§3.8.3)
/// </summary>
public class TreeFruit
{
    public int RuleId { get; set; }
    public string RuleName { get; set; } = "";
    public int KnowledgeLevel { get; set; }
    public bool IsMastered { get; set; }
}

/// <summary>
/// Информация о доступном скине дерева
/// </summary>
public class TreeSkinInfo
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string AssetPrefix { get; set; } = "";
    public string StageEmojis { get; set; } = "";
    public bool IsPremium { get; set; }
    public int PriceCoins { get; set; }
    public bool IsOwned { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>
/// Request to pour user XP into the tree
/// </summary>
public class PourXpRequest
{
    public long Amount { get; set; }
}

/// <summary>
/// Response after pouring XP
/// </summary>
public class PourXpResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public long TreeXpAfter { get; set; }
    public int TreeLevelAfter { get; set; }
    public TreeStage StageAfter { get; set; }
    public bool StageChanged { get; set; }
    public long RemainingUserXp { get; set; }
}
