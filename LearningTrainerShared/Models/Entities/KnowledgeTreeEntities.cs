using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace LearningTrainerShared.Models;

/// <summary>
/// Стадии роста Дерева Знаний (§3.8 LEARNING_IMPROVEMENTS)
/// </summary>
public enum TreeStage
{
    Seed = 0,       // 🌰 Зерно — 0 XP
    Sprout = 1,     // 🌱 Росток — 100+ XP, 10+ слов
    Sapling = 2,    // 🌿 Саженец — 500+ XP, 50+ слов
    YoungTree = 3,  // 🌳 Молодое дерево — 2000+ XP, 200+ слов
    MatureTree = 4, // 🌲 Зрелое дерево — 10000+ XP, 1000+ слов
    MightyTree = 5, // 🏔️ Могучее дерево — 30000+ XP, 3000+ слов
    Legendary = 6   // 🌍 Легендарное дерево — 75000+ XP, 5000+ слов
}

/// <summary>
/// Персональное Дерево Знаний пользователя (§3.8 LEARNING_IMPROVEMENTS).
/// Живая визуализация прогресса — растёт с каждым выученным словом.
/// </summary>
public class KnowledgeTree
{
    [Key]
    public int Id { get; set; }

    public int UserId { get; set; }

    [JsonIgnore]
    public User? User { get; set; }

    public int TreeSkinId { get; set; } = 1; // Default skin

    [JsonIgnore]
    public TreeSkin? TreeSkin { get; set; }

    public TreeStage CurrentStage { get; set; } = TreeStage.Seed;

    /// <summary>
    /// Total words contributing to tree growth (learned words with KL >= 4)
    /// </summary>
    public int TotalWordsContributed { get; set; }

    /// <summary>
    /// Total XP contributing to tree growth
    /// </summary>
    public long TotalXpContributed { get; set; }

    /// <summary>
    /// XP poured manually by user into the tree (separate from TotalXpContributed).
    /// Used for stage/level calculations.
    /// </summary>
    public long TreeXp { get; set; }

    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Is the tree currently wilting (no activity for 7+ days)?
    /// Computed on the client side from LastActivityAt.
    /// </summary>
    [NotMapped]
    public bool IsWilting => (DateTime.UtcNow - LastActivityAt).TotalDays > 7;

    /// <summary>
    /// Days since last activity (for wilting visualization)
    /// </summary>
    [NotMapped]
    public int DaysSinceActivity => (int)(DateTime.UtcNow - LastActivityAt).TotalDays;
}

/// <summary>
/// Скин (визуальный стиль) дерева знаний (§3.8 LEARNING_IMPROVEMENTS).
/// </summary>
public class TreeSkin
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string Name { get; set; } = "";

    /// <summary>
    /// CSS class prefix or asset path prefix for rendering
    /// </summary>
    [MaxLength(100)]
    public string AssetPrefix { get; set; } = "default";

    /// <summary>
    /// Emoji for each growth stage (JSON or pipe-separated)
    /// </summary>
    [MaxLength(200)]
    public string StageEmojis { get; set; } = "🌰|🌱|🌿|🌳|🌲|🏔️|🌍";

    public bool IsPremium { get; set; }

    public int PriceCoins { get; set; }
}
