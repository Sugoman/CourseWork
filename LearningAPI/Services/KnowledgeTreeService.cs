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
    private const double CanvasW = 2000;
    private const double CanvasH = 1800;
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

    private static string CacheKey(int userId) => $"knowledge_tree:v3:{userId}";

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
            CanvasWidth = CanvasW,
            CanvasHeight = CanvasH,
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

        // ─── Build Botanical Dendrogram ─────────────────────────
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

        // Group dictionaries by tag → branches
        var tagGroups = new Dictionary<string, List<int>>();
        for (int i = 0; i < dictionaries.Count; i++)
        {
            var dict = dictionaries[i];
            var tags = string.IsNullOrWhiteSpace(dict.Tags)
                ? new[] { "Без тега" }
                : dict.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (var tag in tags.Take(1)) // first tag only (avoid duplicate dict in multiple branches)
            {
                if (!tagGroups.ContainsKey(tag))
                    tagGroups[tag] = new List<int>();
                tagGroups[tag].Add(i);
            }
        }

        // ─── Botanical layout from bottom-center upward ─────────
        var trunkBaseX = CanvasW / 2;
        var trunkBaseY = CanvasH - 60;   // ground level
        var trunkTopY = CanvasH * 0.40;  // top of trunk
        var trunkTopX = trunkBaseX + 6;  // slight lean for organic feel

        state.Trunk = new BotanicalSegment
        {
            X1 = trunkBaseX, Y1 = trunkBaseY,
            X2 = trunkTopX,  Y2 = trunkTopY
        };

        var branchCount = tagGroups.Count;
        if (branchCount == 0)
            return state;

        // Sort branches: heaviest tags at the bottom of trunk, lightest at top
        var sortedTags = tagGroups
            .Select(kv => (Tag: kv.Key, DictIndices: kv.Value,
                TotalWords: kv.Value.Sum(i => dictionaries[i].TotalWordsCount)))
            .OrderByDescending(x => x.TotalWords)
            .ToList();

        // Global max word count across all tags — used for proportional sizing
        var globalMaxWords = sortedTags.Max(x => x.TotalWords);
        if (globalMaxWords < 1) globalMaxWords = 1;

        var rng = new Random(userId); // deterministic per user

        // Pre-generate organic side pattern: not strict alternation
        // Real trees sometimes have 2-3 branches on the same side in a row
        var sides = new double[sortedTags.Count];
        var lastSide = 1.0;
        var sameCount = 0;
        for (int si = 0; si < sides.Length; si++)
        {
            // After 1-2 branches on same side, switch; occasionally allow 3
            var switchChance = sameCount == 0 ? 0.35 : sameCount == 1 ? 0.65 : 0.90;
            if (rng.NextDouble() < switchChance)
            {
                lastSide = -lastSide;
                sameCount = 0;
            }
            else
            {
                sameCount++;
            }
            sides[si] = lastSide;
        }

        for (int branchIdx = 0; branchIdx < sortedTags.Count; branchIdx++)
        {
            var (tag, dictIndices, tagTotalWords) = sortedTags[branchIdx];

            // ── Weight ratio 0..1 (1 = heaviest branch) + random jitter for liveliness ──
            var weightRatio = (double)tagTotalWords / globalMaxWords;
            var jitter = (rng.NextDouble() - 0.5) * 0.35; // ±17% random variation
            var jitteredWeight = Math.Clamp(weightRatio + jitter, 0.05, 1.0);

            // Attachment position along trunk with organic spacing
            var tBase = sortedTags.Count == 1
                ? 0.50
                : 0.18 + 0.74 * branchIdx / (sortedTags.Count - 1);
            // Larger random offset (±8%) so branches aren't evenly spaced
            var t = Math.Clamp(tBase + (rng.NextDouble() - 0.5) * 0.16, 0.12, 0.96);

            var attachX = trunkBaseX + (trunkTopX - trunkBaseX) * t;
            var attachY = trunkBaseY + (trunkTopY - trunkBaseY) * t;

            // Organic side selection (not strict alternation)
            var side = sides[branchIdx];

            // ── Organic angle: wide range with heavy randomness ──
            // Base angle 25°-55° from vertical, then ±15° random wobble
            var baseAngleDeg = 25.0 + 30.0 * jitteredWeight;
            var angleDeg = baseAngleDeg + (rng.NextDouble() - 0.5) * 30.0;
            angleDeg = Math.Clamp(angleDeg, 20.0, 70.0);
            var angleFromVertical = side * angleDeg;
            var angleRad = (angleFromVertical - 90.0) * Math.PI / 180.0;

            // ── Organic length: wide variance (±30%) ──
            var baseLen = 150.0 + 230.0 * jitteredWeight;
            var branchLen = baseLen * (0.70 + rng.NextDouble() * 0.60); // ±30%

            var tipX = attachX + Math.Cos(angleRad) * branchLen;
            var tipY = attachY + Math.Sin(angleRad) * branchLen;

            // Clamp within canvas
            tipX = Math.Clamp(tipX, 100, CanvasW - 100);
            tipY = Math.Clamp(tipY, 60, attachY - 30); // tip must always be above attach point

            // ── Safe Bezier CPs with organic sway ──
            var dy = attachY - tipY;
            var dx = tipX - attachX;
            // Randomize CP positions more broadly for organic curves
            var cp1Frac = 0.20 + rng.NextDouble() * 0.20; // 20%-40%
            var cp2Frac = 0.55 + rng.NextDouble() * 0.25; // 55%-80%
            var cp1x = attachX + dx * cp1Frac + side * (10.0 + rng.NextDouble() * 30.0);
            var cp1y = attachY - dy * cp1Frac - rng.NextDouble() * 15.0;
            var cp2x = attachX + dx * cp2Frac + side * (rng.NextDouble() * 20.0 - 5.0);
            var cp2y = attachY - dy * cp2Frac - rng.NextDouble() * 10.0;

            // Guarantee monotonic upward progression: cp1y > cp2y > tipY (screen coords)
            cp1y = Math.Clamp(cp1y, tipY, attachY);
            cp2y = Math.Clamp(cp2y, tipY, cp1y);

            var tagDicts = dictIndices.Select(i => dictionaries[i]).ToList();
            var tagTotal = tagDicts.Sum(d => d.TotalWordsCount);
            var tagLearned = tagDicts.Sum(d => d.LearnedCount);

            // ── Dynamic stroke-width: proportional + random variation (6px–18px) ──
            var branchStroke = Math.Clamp(6.0 + 12.0 * jitteredWeight + (rng.NextDouble() - 0.5) * 2.0, 6, 18);

            var branch = new TreeBranch
            {
                Id = $"branch-{tag.GetHashCode():x8}",
                TagName = tag,
                Segment = new BotanicalSegment { X1 = attachX, Y1 = attachY, X2 = tipX, Y2 = tipY },
                Cp1X = cp1x, Cp1Y = cp1y,
                Cp2X = cp2x, Cp2Y = cp2y,
                StrokeWidth = branchStroke,
                TotalWords = tagTotal,
                LearnedWords = tagLearned,
                Progress = tagTotal > 0 ? (double)tagLearned / tagTotal : 0,
            };

            // ─── Twigs (dictionaries) off this branch ───────────
            // Sort twigs by word count DESCENDING: heaviest attach near trunk (low t), lightest near tip (high t)
            var sortedDicts = tagDicts
                .OrderByDescending(d => d.TotalWordsCount)
                .ToList();
            var twigCount = sortedDicts.Count;
            var branchMaxWords = sortedDicts.Count > 0 ? sortedDicts[0].TotalWordsCount : 1;
            if (branchMaxWords < 1) branchMaxWords = 1;

            // ── Even angular fan: distribute twigs across ±50° arc relative to branch direction ──
            var branchAngle = Math.Atan2(tipY - attachY, tipX - attachX);

            for (int ti = 0; ti < twigCount; ti++)
            {
                var dict = sortedDicts[ti];

                // Twig weight within this branch (1 = heaviest twig) + jitter
                var twigWeightRaw = (double)dict.TotalWordsCount / branchMaxWords;
                var twigJitter = (rng.NextDouble() - 0.5) * 0.25;
                var twigWeight = Math.Clamp(twigWeightRaw + twigJitter, 0.05, 1.0);

                // Heaviest twig (ti=0) at t≈0.20, lightest at t≈0.85, with random offset
                var twigTBase = twigCount == 1
                    ? 0.55
                    : 0.20 + 0.65 * ti / (twigCount - 1);
                var twigT = Math.Clamp(twigTBase + (rng.NextDouble() - 0.5) * 0.06, 0.15, 0.90);

                // Point on the parent branch Bezier curve
                var twAttachX = Bezier(attachX, cp1x, cp2x, tipX, twigT);
                var twAttachY = Bezier(attachY, cp1y, cp2y, tipY, twigT);

                // ── Alternating sides: twigs grow left/right of branch like a real tree ──
                var twigSide = (ti % 2 == 0) ? 1.0 : -1.0;
                if (rng.NextDouble() < 0.20) twigSide = -twigSide; // occasional same-side pair
                // Angle offset from branch: 20°-55° perpendicular, with weight & randomness
                var twigOffsetRad = (20.0 + 35.0 * (1.0 - twigWeight * 0.4)
                    + (rng.NextDouble() - 0.5) * 18.0) * Math.PI / 180.0;
                var twigAngle = branchAngle + twigSide * twigOffsetRad
                    + (rng.NextDouble() - 0.5) * 0.12;

                // ── Dynamic twig length: heavy twigs longer, light shorter, with ±20% jitter ──
                var twigBaseLen = 60.0 + 100.0 * twigWeight;
                var twigLen = twigBaseLen * (0.80 + rng.NextDouble() * 0.40);

                var twTipX = twAttachX + Math.Cos(twigAngle) * twigLen;
                var twTipY = twAttachY + Math.Sin(twigAngle) * twigLen;

                // Tip must be above attach (upward growth)
                twTipY = Math.Min(twTipY, twAttachY - 15);
                twTipX = Math.Clamp(twTipX, 60, CanvasW - 60);
                twTipY = Math.Clamp(twTipY, 40, CanvasH - 100);

                // ── Curved twig Bezier CPs: perpendicular sway for organic curvature ──
                var twDy = twAttachY - twTipY;
                var twDx = twTipX - twAttachX;
                var twigPerpAngle = twigAngle + Math.PI / 2.0;
                // Random sway direction & magnitude perpendicular to twig
                var curveSway = (rng.NextDouble() < 0.5 ? 1.0 : -1.0)
                    * (12.0 + rng.NextDouble() * 24.0); // 12-36px
                var twCp1x = twAttachX + twDx * 0.33
                    + Math.Cos(twigPerpAngle) * curveSway * 0.8;
                var twCp1y = twAttachY - twDy * (0.30 + rng.NextDouble() * 0.10)
                    + Math.Sin(twigPerpAngle) * curveSway * 0.8;
                var twCp2x = twAttachX + twDx * 0.66
                    + Math.Cos(twigPerpAngle) * curveSway * 0.4;
                var twCp2y = twAttachY - twDy * (0.60 + rng.NextDouble() * 0.10)
                    + Math.Sin(twigPerpAngle) * curveSway * 0.4;

                // Clamp CPs monotonically: attachY >= cp1y >= cp2y >= tipY
                twCp1y = Math.Clamp(twCp1y, twTipY, twAttachY);
                twCp2y = Math.Clamp(twCp2y, twTipY, twCp1y);

                // ── Dynamic twig stroke-width: proportional (2.5px–7px) ──
                var twigStroke = Math.Clamp(2.5 + 4.5 * twigWeight, 2.5, 7);

                var twig = new TreeTwig
                {
                    Id = $"twig-{dict.Id}",
                    DictionaryId = dict.Id,
                    DictionaryName = dict.Name,
                    Segment = new BotanicalSegment { X1 = twAttachX, Y1 = twAttachY, X2 = twTipX, Y2 = twTipY },
                    Cp1X = twCp1x, Cp1Y = twCp1y,
                    Cp2X = twCp2x, Cp2Y = twCp2y,
                    StrokeWidth = twigStroke,
                    TotalWords = dict.TotalWordsCount,
                    LearnedWords = dict.LearnedCount,
                    Progress = dict.TotalWordsCount > 0 ? (double)dict.LearnedCount / dict.TotalWordsCount : 0,
                };

                // ─── Sprigs (mini-branches) along twig Bezier holding leaves ───
                var leafCount = Math.Min(dict.Words.Count, MaxLeavesPerTwig);
                var sprigCount = Math.Max(2, (int)Math.Ceiling(leafCount / 4.0));
                sprigCount = Math.Min(sprigCount, 14);
                var leafIdx = 0;

                // 20% of leaves go directly on the twig for fullness
                var twigLeafCount = Math.Max(1, (int)(leafCount * 0.20));
                var sprigLeafCount = leafCount - twigLeafCount;

                // ── First: place sprig leaves (80%) ──
                for (int ci = 0; ci < sprigCount && leafIdx < sprigLeafCount; ci++)
                {
                    // Sprig attachment point on twig Bezier
                    var sbt = 0.20 + 0.78 * ci / Math.Max(sprigCount - 1, 1);
                    var spBaseX = Bezier(twAttachX, twCp1x, twCp2x, twTipX, sbt);
                    var spBaseY = Bezier(twAttachY, twCp1y, twCp2y, twTipY, sbt);

                    // Sprig grows perpendicular to twig, alternating sides
                    var sprigSide = (ci % 2 == 0) ? 1.0 : -1.0;
                    if (rng.NextDouble() < 0.18) sprigSide = -sprigSide;
                    var sprigAngle = twigAngle + sprigSide * (Math.PI / 2.0)
                        + (rng.NextDouble() - 0.5) * 0.44;
                    var sprigLen = 10.0 + rng.NextDouble() * 25.0;
                    var spTipX = spBaseX + Math.Cos(sprigAngle) * sprigLen;
                    var spTipY = spBaseY + Math.Sin(sprigAngle) * sprigLen;

                    // Quadratic Bezier CP: perpendicular offset for slight curvature
                    var spMidX = (spBaseX + spTipX) / 2.0;
                    var spMidY = (spBaseY + spTipY) / 2.0;
                    var spPerpAngle = sprigAngle + Math.PI / 2.0;
                    var spCurve = (rng.NextDouble() < 0.5 ? 1.0 : -1.0)
                        * (3.0 + rng.NextDouble() * 8.0); // 3-11px sway
                    var spCpX = spMidX + Math.Cos(spPerpAngle) * spCurve;
                    var spCpY = spMidY + Math.Sin(spPerpAngle) * spCurve;

                    var sprigStroke = 1.0 + rng.NextDouble() * 1.5;

                    var sprig = new TreeSprig
                    {
                        X1 = spBaseX, Y1 = spBaseY,
                        X2 = spTipX,  Y2 = spTipY,
                        CpX = spCpX,  CpY = spCpY,
                        StrokeWidth = sprigStroke,
                    };

                    var leavesHere = ci == sprigCount - 1
                        ? sprigLeafCount - leafIdx
                        : Math.Min(2 + (int)(rng.NextDouble() * 4), sprigLeafCount - leafIdx);

                    for (int cli = 0; cli < leavesHere && leafIdx < sprigLeafCount; cli++)
                    {
                        var w = dict.Words[leafIdx];
                        var la = rng.NextDouble() * 2.0 * Math.PI;
                        var ld = 1.5 + rng.NextDouble() * 6.0;
                        var lx = spTipX + Math.Cos(la) * ld;
                        var ly = spTipY + Math.Sin(la) * ld;
                        var leafRot = sprigAngle * 180.0 / Math.PI
                                      + (rng.NextDouble() - 0.5) * 70.0;

                        sprig.Leaves.Add(new TreeLeaf
                        {
                            WordId = w.Id,
                            Word = w.OriginalWord,
                            IsLearned = w.IsLearned,
                            X = lx, Y = ly,
                            Rotation = leafRot,
                        });
                        leafIdx++;
                    }

                    twig.Sprigs.Add(sprig);
                }

                // ── Then: place 20% leaves directly on the twig Bezier for fullness ──
                for (int tli = 0; tli < twigLeafCount && leafIdx < leafCount; tli++)
                {
                    var w = dict.Words[leafIdx];
                    var twLeafT = 0.15 + 0.80 * rng.NextDouble();
                    var twLeafBaseX = Bezier(twAttachX, twCp1x, twCp2x, twTipX, twLeafT);
                    var twLeafBaseY = Bezier(twAttachY, twCp1y, twCp2y, twTipY, twLeafT);
                    // Offset slightly to each side of twig
                    var twLeafPerp = twigAngle + Math.PI / 2.0;
                    var twLeafSide = (tli % 2 == 0) ? 1.0 : -1.0;
                    var twLeafDist = 2.0 + rng.NextDouble() * 8.0;
                    var lx = twLeafBaseX + Math.Cos(twLeafPerp) * twLeafDist * twLeafSide;
                    var ly = twLeafBaseY + Math.Sin(twLeafPerp) * twLeafDist * twLeafSide;
                    var leafRot = twigAngle * 180.0 / Math.PI
                                  + twLeafSide * 20.0 + (rng.NextDouble() - 0.5) * 60.0;

                    twig.Leaves.Add(new TreeLeaf
                    {
                        WordId = w.Id,
                        Word = w.OriginalWord,
                        IsLearned = w.IsLearned,
                        X = lx, Y = ly,
                        Rotation = leafRot,
                    });
                    leafIdx++;
                }

                branch.Twigs.Add(twig);
            }

            state.Branches.Add(branch);
        }

        return state;
    }

    /// <summary>Cubic bezier interpolation for a single axis</summary>
    private static double Bezier(double p0, double p1, double p2, double p3, double t)
    {
        var u = 1 - t;
        return u * u * u * p0 + 3 * u * u * t * p1 + 3 * u * t * t * p2 + t * t * t * p3;
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
