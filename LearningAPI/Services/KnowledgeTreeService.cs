using LearningAPI.Extensions;
using LearningTrainerShared.Context;
using LearningTrainerShared.Models;
using LearningTrainerShared.Models.KnowledgeTreeDto;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace LearningAPI.Services;

public interface IKnowledgeTreeService
{
    Task<KnowledgeTreeState> GetTreeStateAsync(int userId, CancellationToken ct = default);
    Task<PourXpResponse> PourXpAsync(int userId, long amount, CancellationToken ct = default);
    Task<List<TreeSkinInfo>> GetAvailableSkinsAsync(int userId, CancellationToken ct = default);
    Task<bool> ChangeSkinAsync(int userId, int skinId, CancellationToken ct = default);
    Task UpdateTreeAsync(int userId, CancellationToken ct = default);
}

public class KnowledgeTreeService : IKnowledgeTreeService
{
    private readonly ApiDbContext _context;
    private readonly IDistributedCache _cache;
    private readonly ILogger<KnowledgeTreeService> _logger;

    private const int MaxLeavesPerTwig = 100;
    private const int DefaultMaxVisibleBranches = 6;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    private static readonly (TreeStage Stage, long XpThreshold, int WordsThreshold, string Name)[] StageThresholds =
    [
        (TreeStage.Seed,       0,     0,    "Зерно"),
        (TreeStage.Sprout,     100,   10,   "Росток"),
        (TreeStage.Sapling,    500,   50,   "Саженец"),
        (TreeStage.YoungTree,  2000,  200,  "Молодое дерево"),
        (TreeStage.MatureTree, 10000, 1000, "Зрелое дерево"),
        (TreeStage.MightyTree, 30000, 3000, "Могучее дерево"),
        (TreeStage.Legendary,  75000, 5000, "Легендарное дерево"),
    ];

    public KnowledgeTreeService(ApiDbContext context, IDistributedCache cache, ILogger<KnowledgeTreeService> logger)
    {
        _context = context;
        _cache = cache;
        _logger = logger;
    }

    private static string CacheKey(int userId) => $"knowledge_tree:{userId}";

    public async Task<KnowledgeTreeState> GetTreeStateAsync(int userId, CancellationToken ct = default)
    {
        // Try Redis cache first
        var cached = await _cache.TryGetStringAsync(CacheKey(userId));
        if (cached != null)
        {
            var cachedState = JsonSerializer.Deserialize<KnowledgeTreeState>(cached);
            if (cachedState != null)
                return cachedState;
        }

        var state = await BuildTreeStateAsync(userId, ct);

        // Cache in Redis
        await _cache.TrySetStringAsync(
            CacheKey(userId),
            JsonSerializer.Serialize(state),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheDuration });

        return state;
    }

    public async Task<PourXpResponse> PourXpAsync(int userId, long amount, CancellationToken ct = default)
    {
        if (amount <= 0)
            return new PourXpResponse { Success = false, Message = "Количество XP должно быть положительным" };

        var userStats = await _context.UserStats.FindAsync([userId], ct);
        if (userStats == null)
            return new PourXpResponse { Success = false, Message = "Статистика пользователя не найдена" };

        if (userStats.TotalXp < amount)
            return new PourXpResponse { Success = false, Message = "Недостаточно XP" };

        var tree = await GetOrCreateTreeAsync(userId, ct);
        var oldStage = tree.CurrentStage;

        // Deduct from user, add to tree
        userStats.TotalXp -= amount;
        tree.TreeXp += amount;
        tree.TotalXpContributed += amount;
        tree.LastActivityAt = DateTime.UtcNow;

        // Recalculate stage based on TreeXp + words
        var learnedWords = await _context.LearningProgresses
            .Where(p => p.UserId == userId && p.KnowledgeLevel >= 4)
            .CountAsync(ct);
        tree.TotalWordsContributed = learnedWords;

        var newStage = CalculateStage(tree.TreeXp, learnedWords);
        tree.CurrentStage = newStage;

        await _context.SaveChangesAsync(ct);

        // Invalidate cache
        await _cache.TryRemoveAsync(CacheKey(userId));

        var stageChanged = newStage != oldStage;
        if (stageChanged)
        {
            _logger.LogInformation("User {UserId} tree grew from {Old} to {New} after pouring {Xp} XP",
                userId, oldStage, newStage, amount);
        }

        return new PourXpResponse
        {
            Success = true,
            Message = stageChanged ? $"Дерево выросло до стадии «{GetStageName(newStage)}»!" : "XP влито в дерево!",
            TreeXpAfter = tree.TreeXp,
            TreeLevelAfter = CalculateTreeLevel(tree.TreeXp),
            StageAfter = newStage,
            StageChanged = stageChanged,
            RemainingUserXp = userStats.TotalXp
        };
    }

    public async Task<List<TreeSkinInfo>> GetAvailableSkinsAsync(int userId, CancellationToken ct = default)
    {
        var tree = await GetOrCreateTreeAsync(userId, ct);

        return await _context.TreeSkins
            .Select(s => new TreeSkinInfo
            {
                Id = s.Id,
                Name = s.Name,
                AssetPrefix = s.AssetPrefix,
                StageEmojis = s.StageEmojis,
                IsPremium = s.IsPremium,
                PriceCoins = s.PriceCoins,
                IsOwned = !s.IsPremium,
                IsActive = s.Id == tree.TreeSkinId
            })
            .ToListAsync(ct);
    }

    public async Task<bool> ChangeSkinAsync(int userId, int skinId, CancellationToken ct = default)
    {
        var skin = await _context.TreeSkins.FindAsync([skinId], ct);
        if (skin == null) return false;

        var tree = await GetOrCreateTreeAsync(userId, ct);
        tree.TreeSkinId = skinId;
        await _context.SaveChangesAsync(ct);

        await _cache.TryRemoveAsync(CacheKey(userId));
        _logger.LogInformation("User {UserId} changed tree skin to {SkinId} ({SkinName})", userId, skinId, skin.Name);
        return true;
    }

    public async Task UpdateTreeAsync(int userId, CancellationToken ct = default)
    {
        var tree = await GetOrCreateTreeAsync(userId, ct);

        var learnedWords = await _context.LearningProgresses
            .Where(p => p.UserId == userId && p.KnowledgeLevel >= 4)
            .CountAsync(ct);

        tree.TotalWordsContributed = learnedWords;
        tree.LastActivityAt = DateTime.UtcNow;

        var newStage = CalculateStage(tree.TreeXp, learnedWords);
        if (newStage != tree.CurrentStage)
        {
            _logger.LogInformation("User {UserId} tree grew from {OldStage} to {NewStage}!",
                userId, tree.CurrentStage, newStage);
            tree.CurrentStage = newStage;
        }

        await _context.SaveChangesAsync(ct);
        await _cache.TryRemoveAsync(CacheKey(userId));
    }

    // ─── Private helpers ───────────────────────────────────────────

    private async Task<KnowledgeTreeState> BuildTreeStateAsync(int userId, CancellationToken ct)
    {
        var tree = await GetOrCreateTreeAsync(userId, ct);
        var skin = await _context.TreeSkins.FindAsync([tree.TreeSkinId], ct)
                   ?? await _context.TreeSkins.FindAsync([1], ct);

        var userStats = await _context.UserStats.FindAsync([userId], ct);

        var state = new KnowledgeTreeState
        {
            CurrentStage = tree.CurrentStage,
            StageName = GetStageName(tree.CurrentStage),
            StageEmoji = GetStageEmoji(skin!, tree.CurrentStage),
            TotalWordsContributed = tree.TotalWordsContributed,
            TotalXpContributed = tree.TotalXpContributed,
            TreeXp = tree.TreeXp,
            TreeLevel = CalculateTreeLevel(tree.TreeXp),
            AvailableUserXp = userStats?.TotalXp ?? 0,
            TreeSkinId = tree.TreeSkinId,
            TreeSkinName = skin!.Name,
            AssetPrefix = skin.AssetPrefix,
            LastActivityAt = tree.LastActivityAt,
            IsWilting = tree.IsWilting,
            DaysSinceActivity = tree.DaysSinceActivity,
            MaxVisibleBranches = DefaultMaxVisibleBranches,
        };

        // Progress to next stage
        var nextIdx = (int)tree.CurrentStage + 1;
        if (nextIdx < StageThresholds.Length)
        {
            var next = StageThresholds[nextIdx];
            state.WordsForNextStage = next.WordsThreshold;
            state.XpForNextStage = next.XpThreshold;

            var wordProgress = next.WordsThreshold > 0
                ? (double)tree.TotalWordsContributed / next.WordsThreshold * 100
                : 100;
            var xpProgress = next.XpThreshold > 0
                ? (double)tree.TreeXp / next.XpThreshold * 100
                : 100;
            state.ProgressToNextStage = Math.Min(100, Math.Min(wordProgress, xpProgress));
        }
        else
        {
            state.ProgressToNextStage = 100;
        }

        // Build Branches (by Tag) → Twigs (Dictionaries) → Leaves (Words)
        var dictionaries = await _context.Dictionaries
            .Where(d => d.UserId == userId)
            .Select(d => new
            {
                d.Id,
                d.Name,
                d.Tags,
                Words = d.Words.Select(w => new
                {
                    w.Id,
                    w.OriginalWord,
                    IsLearned = w.Progress.Any(p => p.UserId == userId && p.KnowledgeLevel >= 4)
                }).Take(MaxLeavesPerTwig).ToList(),
                TotalWordsCount = d.Words.Count,
                LearnedCount = d.Words.Count(w =>
                    w.Progress.Any(p => p.UserId == userId && p.KnowledgeLevel >= 4))
            })
            .ToListAsync(ct);

        // Group dictionaries by tag
        var tagGroups = new Dictionary<string, List<TreeTwig>>();

        foreach (var dict in dictionaries)
        {
            var tags = string.IsNullOrWhiteSpace(dict.Tags)
                ? new[] { "Без тега" }
                : dict.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            var twig = new TreeTwig
            {
                DictionaryId = dict.Id,
                DictionaryName = dict.Name,
                TotalWords = dict.TotalWordsCount,
                LearnedWords = dict.LearnedCount,
                CompletionPercent = dict.TotalWordsCount > 0
                    ? dict.LearnedCount * 100.0 / dict.TotalWordsCount
                    : 0,
                Leaves = dict.Words.Select(w => new TreeLeaf
                {
                    WordId = w.Id,
                    Word = w.OriginalWord,
                    IsLearned = w.IsLearned
                }).ToList()
            };

            foreach (var tag in tags)
            {
                if (!tagGroups.ContainsKey(tag))
                    tagGroups[tag] = new List<TreeTwig>();
                tagGroups[tag].Add(twig);
            }
        }

        state.Branches = tagGroups
            .Select(g => new TreeBranch
            {
                TagName = g.Key,
                Twigs = g.Value,
                TotalWords = g.Value.Sum(t => t.TotalWords),
                LearnedWords = g.Value.Sum(t => t.LearnedWords),
                CompletionPercent = g.Value.Sum(t => t.TotalWords) > 0
                    ? g.Value.Sum(t => t.LearnedWords) * 100.0 / g.Value.Sum(t => t.TotalWords)
                    : 0
            })
            .OrderByDescending(b => b.TotalWords)
            .Take(DefaultMaxVisibleBranches)
            .ToList();

        // Fruits = grammar rules
        state.Fruits = await _context.GrammarProgresses
            .Where(gp => gp.UserId == userId)
            .Select(gp => new TreeFruit
            {
                RuleId = gp.RuleId,
                RuleName = gp.Rule!.Title,
                KnowledgeLevel = gp.KnowledgeLevel,
                IsMastered = gp.KnowledgeLevel >= 4
            })
            .ToListAsync(ct);

        return state;
    }

    private static TreeStage CalculateStage(long treeXp, int learnedWords)
    {
        var stage = TreeStage.Seed;
        for (var i = StageThresholds.Length - 1; i >= 0; i--)
        {
            if (treeXp >= StageThresholds[i].XpThreshold &&
                learnedWords >= StageThresholds[i].WordsThreshold)
            {
                stage = StageThresholds[i].Stage;
                break;
            }
        }
        return stage;
    }

    private static int CalculateTreeLevel(long treeXp)
    {
        // Simple level formula: level = floor(sqrt(treeXp / 50)) + 1
        return (int)Math.Floor(Math.Sqrt(treeXp / 50.0)) + 1;
    }

    private async Task<LearningTrainerShared.Models.KnowledgeTree> GetOrCreateTreeAsync(int userId, CancellationToken ct)
    {
        var tree = await _context.KnowledgeTrees
            .FirstOrDefaultAsync(t => t.UserId == userId, ct);

        if (tree == null)
        {
            tree = new LearningTrainerShared.Models.KnowledgeTree
            {
                UserId = userId,
                TreeSkinId = 1,
                CurrentStage = TreeStage.Seed,
                CreatedAt = DateTime.UtcNow,
                LastActivityAt = DateTime.UtcNow
            };
            _context.KnowledgeTrees.Add(tree);
            await _context.SaveChangesAsync(ct);
        }

        return tree;
    }

    private static string GetStageName(TreeStage stage)
    {
        foreach (var t in StageThresholds)
        {
            if (t.Stage == stage) return t.Name;
        }
        return "Зерно";
    }

    private static string GetStageEmoji(TreeSkin skin, TreeStage stage)
    {
        var emojis = skin.StageEmojis.Split('|');
        var idx = (int)stage;
        return idx < emojis.Length ? emojis[idx] : "🌳";
    }
}
