namespace LearningTrainerShared.Models.KnowledgeTreeDto;

/// <summary>
/// Полное состояние Дерева Знаний — Botanical Dendrogram
/// </summary>
public class KnowledgeTreeState
{
    // === ОСНОВНОЕ СОСТОЯНИЕ ===
    public TreeStage CurrentStage { get; set; }
    public string StageName { get; set; } = "";
    public string StageEmoji { get; set; } = "";
    public int TotalWordsContributed { get; set; }
    public long TotalXpContributed { get; set; }

    /// <summary>Tree-specific XP (poured by user)</summary>
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

    // === BOTANICAL DENDROGRAM ===
    /// <summary>Trunk segment (Root → first fork)</summary>
    public BotanicalSegment Trunk { get; set; } = new();

    /// <summary>Branches (Tags) growing off trunk</summary>
    public List<TreeBranch> Branches { get; set; } = new();

    /// <summary>SVG canvas dimensions</summary>
    public double CanvasWidth { get; set; } = 2000;
    public double CanvasHeight { get; set; } = 1800;

    public int MaxVisibleBranches { get; set; } = 6;
}

/// <summary>
/// A line segment in the tree, defined by start/end points.
/// Used for trunk, branches, twigs.
/// </summary>
public class BotanicalSegment
{
    public double X1 { get; set; }
    public double Y1 { get; set; }
    public double X2 { get; set; }
    public double Y2 { get; set; }
}

/// <summary>
/// A branch = tag grouping. Grows off the trunk.
/// </summary>
public class TreeBranch
{
    public string Id { get; set; } = "";
    public string TagName { get; set; } = "";

    /// <summary>Start point (on trunk) and end point (tip)</summary>
    public BotanicalSegment Segment { get; set; } = new();

    /// <summary>Bezier control-point offsets for organic curvature</summary>
    public double Cp1X { get; set; }
    public double Cp1Y { get; set; }
    public double Cp2X { get; set; }
    public double Cp2Y { get; set; }

    public double StrokeWidth { get; set; } = 12;
    public int TotalWords { get; set; }
    public int LearnedWords { get; set; }
    public double Progress { get; set; }

    public List<TreeTwig> Twigs { get; set; } = new();
}

/// <summary>
/// A twig = dictionary. Grows off a branch.
/// </summary>
public class TreeTwig
{
    public string Id { get; set; } = "";
    public int DictionaryId { get; set; }
    public string DictionaryName { get; set; } = "";

    public BotanicalSegment Segment { get; set; } = new();
    public double Cp1X { get; set; }
    public double Cp1Y { get; set; }
    public double Cp2X { get; set; }
    public double Cp2Y { get; set; }

    public double StrokeWidth { get; set; } = 5;
    public int TotalWords { get; set; }
    public int LearnedWords { get; set; }
    public double Progress { get; set; }

    /// <summary>Leaf positions at the tip (word-level)</summary>
    public List<TreeLeaf> Leaves { get; set; } = new();

    /// <summary>Decorative mini-branches that hold leaf clusters</summary>
    public List<TreeSprig> Sprigs { get; set; } = new();
}

/// <summary>
/// Decorative mini-branch growing off a twig. Holds a small cluster of leaves.
/// </summary>
public class TreeSprig
{
    public double X1 { get; set; }
    public double Y1 { get; set; }
    public double X2 { get; set; }
    public double Y2 { get; set; }
    /// <summary>Quadratic Bezier control point for slight curvature</summary>
    public double CpX { get; set; }
    public double CpY { get; set; }
    public double StrokeWidth { get; set; } = 1.5;
    /// <summary>Leaves attached to this sprig tip</summary>
    public List<TreeLeaf> Leaves { get; set; } = new();
}

/// <summary>
/// A leaf = word endpoint. Rendered as a green leaf icon at (X, Y).
/// </summary>
public class TreeLeaf
{
    public int WordId { get; set; }
    public string Word { get; set; } = "";
    public bool IsLearned { get; set; }

    /// <summary>Position at the end of the twig (or scattered around it)</summary>
    public double X { get; set; }
    public double Y { get; set; }

    /// <summary>Rotation angle in degrees for visual variety</summary>
    public double Rotation { get; set; }
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
