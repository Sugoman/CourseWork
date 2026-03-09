using LearningAPI.Extensions;
using LearningTrainerShared.Context;
using LearningTrainerShared.Models;
using LearningTrainerShared.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace LearningAPI.Controllers
{
    /// <summary>
    /// Контроллер интерактивной грамматики (§17 LEARNING_IMPROVEMENTS).
    /// Управляет деревом навыков, сессиями практики и прогрессом.
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/grammar")]
    public class GrammarController : BaseApiController
    {
        private readonly ApiDbContext _context;
        private readonly ISpacedRepetitionService _srs;
        private readonly IDistributedCache _cache;

        public GrammarController(ApiDbContext context, ISpacedRepetitionService srs, IDistributedCache cache)
        {
            _context = context;
            _srs = srs;
            _cache = cache;
        }

        /// <summary>
        /// Получить дерево навыков с прогрессом пользователя.
        /// </summary>
        [HttpGet("skill-tree")]
        public async Task<IActionResult> GetSkillTree()
        {
            var userId = GetUserId();

            // Получаем все правила пользователя (собственные + скачанные + shared)
            var sharedRuleIds = await _context.RuleSharings
                .Where(rs => rs.StudentId == userId)
                .Select(rs => rs.RuleId)
                .ToListAsync();

            var rules = await _context.Rules
                .IgnoreQueryFilters()
                .Where(r => r.UserId == userId || sharedRuleIds.Contains(r.Id))
                .Select(r => new
                {
                    r.Id,
                    r.Title,
                    r.Category,
                    r.DifficultyLevel,
                    r.SkillTreeLevel,
                    r.IconEmoji,
                    r.SkillSummary,
                    r.XpReward,
                    r.PrerequisiteRuleIdsJson,
                    ExerciseCount = r.Exercises.Count
                })
                .ToListAsync();

            // Получаем прогресс по всем правилам
            var progress = await _context.GrammarProgresses
                .IgnoreQueryFilters()
                .Where(gp => gp.UserId == userId)
                .ToDictionaryAsync(gp => gp.RuleId, gp => new
                {
                    gp.KnowledgeLevel,
                    gp.NextReview,
                    gp.TotalSessions,
                    gp.CorrectAnswers,
                    gp.TotalAnswers,
                    gp.LastPracticeDate,
                    gp.LapseCount
                });

            var result = rules.Select(r =>
            {
                progress.TryGetValue(r.Id, out var p);
                int[] prereqs = Array.Empty<int>();
                if (!string.IsNullOrEmpty(r.PrerequisiteRuleIdsJson))
                {
                    try { prereqs = JsonSerializer.Deserialize<int[]>(r.PrerequisiteRuleIdsJson) ?? Array.Empty<int>(); }
                    catch { }
                }

                // Проверяем разблокированность: все предпосылки >= KnowledgeLevel 3
                bool isUnlocked = prereqs.Length == 0 || prereqs.All(pid =>
                    progress.ContainsKey(pid) && progress[pid].KnowledgeLevel >= 3);

                return new
                {
                    r.Id,
                    r.Title,
                    r.Category,
                    r.DifficultyLevel,
                    r.SkillTreeLevel,
                    r.IconEmoji,
                    r.SkillSummary,
                    r.XpReward,
                    r.ExerciseCount,
                    PrerequisiteRuleIds = prereqs,
                    IsUnlocked = isUnlocked,
                    KnowledgeLevel = p?.KnowledgeLevel ?? 0,
                    NextReview = p?.NextReview,
                    TotalSessions = p?.TotalSessions ?? 0,
                    AccuracyPercent = (p?.TotalAnswers ?? 0) > 0
                        ? Math.Round((double)(p?.CorrectAnswers ?? 0) / p!.TotalAnswers * 100, 1)
                        : 0.0,
                    LastPracticeDate = p?.LastPracticeDate
                };
            })
            .OrderBy(r => r.SkillTreeLevel)
            .ThenBy(r => r.DifficultyLevel)
            .ToList();

            return Ok(result);
        }

        /// <summary>
        /// Получить правила, которые нужно повторить сегодня.
        /// </summary>
        [HttpGet("due-reviews")]
        public async Task<IActionResult> GetDueReviews()
        {
            var userId = GetUserId();
            var now = DateTime.UtcNow;

            var dueRaw = await _context.GrammarProgresses
                .IgnoreQueryFilters()
                .Where(gp => gp.UserId == userId && gp.NextReview != null && gp.NextReview <= now)
                .Include(gp => gp.Rule)
                .OrderBy(gp => gp.NextReview)
                .Take(20)
                .Select(gp => new
                {
                    gp.RuleId,
                    RuleTitle = gp.Rule!.Title,
                    RuleCategory = gp.Rule.Category,
                    gp.Rule.IconEmoji,
                    gp.KnowledgeLevel,
                    gp.NextReview,
                    gp.TotalSessions,
                    gp.CorrectAnswers,
                    gp.TotalAnswers
                })
                .ToListAsync();

            var dueProgress = dueRaw.Select(gp => new
            {
                gp.RuleId,
                gp.RuleTitle,
                gp.RuleCategory,
                gp.IconEmoji,
                gp.KnowledgeLevel,
                gp.NextReview,
                gp.TotalSessions,
                AccuracyPercent = gp.TotalAnswers > 0
                    ? Math.Round((double)gp.CorrectAnswers / gp.TotalAnswers * 100, 1)
                    : 0.0,
                OverdueDays = gp.NextReview.HasValue
                    ? (int)(now - gp.NextReview.Value).TotalDays
                    : (int?)null
            }).ToList();

            return Ok(new
            {
                Count = dueProgress.Count,
                Rules = dueProgress
            });
        }

        /// <summary>
        /// Начать сессию практики по правилу (возвращает упражнения).
        /// </summary>
        [HttpGet("{ruleId:int}/practice")]
        public async Task<IActionResult> StartPractice(int ruleId, [FromQuery] int count = 10)
        {
            var userId = GetUserId();

            var rule = await _context.Rules
                .IgnoreQueryFilters()
                .Include(r => r.Exercises)
                .FirstOrDefaultAsync(r => r.Id == ruleId &&
                    (r.UserId == userId || _context.RuleSharings.Any(rs => rs.RuleId == r.Id && rs.StudentId == userId)));

            if (rule == null)
                return NotFound("Правило не найдено");

            // Получаем прогресс для адаптивного подбора
            var progress = await _context.GrammarProgresses
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(gp => gp.UserId == userId && gp.RuleId == ruleId);

            int knowledgeLevel = progress?.KnowledgeLevel ?? 0;

            // Фильтруем упражнения по уровню (адаптивность)
            int maxTier = knowledgeLevel switch
            {
                0 or 1 => 1,
                2 or 3 => 2,
                _ => 3
            };

            var exercises = rule.Exercises
                .Where(e => e.DifficultyTier <= maxTier)
                .OrderBy(_ => Random.Shared.Next())
                .Take(count)
                .Select(e => new
                {
                    e.Id,
                    e.ExerciseType,
                    e.Question,
                    e.OptionsJson,
                    e.CorrectIndex,
                    e.CorrectAnswer,
                    e.AlternativeAnswersJson,
                    e.IncorrectSentence,
                    e.ShuffledWordsJson,
                    e.Explanation,
                    e.DifficultyTier,
                    e.OrderIndex
                })
                .ToList();

            return Ok(new
            {
                RuleId = ruleId,
                RuleTitle = rule.Title,
                KnowledgeLevel = knowledgeLevel,
                Exercises = exercises
            });
        }

        /// <summary>
        /// Отправить результат сессии (обновляет GrammarProgress через SM-2).
        /// </summary>
        [HttpPost("{ruleId:int}/submit-session")]
        public async Task<IActionResult> SubmitSession(int ruleId, [FromBody] GrammarSessionResult session)
        {
            var userId = GetUserId();

            // Проверяем что правило существует и доступно
            var ruleExists = await _context.Rules
                .IgnoreQueryFilters()
                .AnyAsync(r => r.Id == ruleId &&
                    (r.UserId == userId || _context.RuleSharings.Any(rs => rs.RuleId == r.Id && rs.StudentId == userId)));

            if (!ruleExists)
                return NotFound("Правило не найдено");

            if (session.Answers == null || session.Answers.Count == 0)
                return BadRequest("Нет ответов");

            int correctCount = session.Answers.Count(a => a.IsCorrect);
            int totalCount = session.Answers.Count;

            // Получить или создать GrammarProgress
            var progress = await _context.GrammarProgresses
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(gp => gp.UserId == userId && gp.RuleId == ruleId);

            if (progress == null)
            {
                progress = new GrammarProgress
                {
                    UserId = userId,
                    RuleId = ruleId
                };
                _context.GrammarProgresses.Add(progress);
            }

            int previousLevel = progress.KnowledgeLevel;

            // Применяем SM-2
            _srs.ApplyGrammarSession(progress, correctCount, totalCount);

            await _context.SaveChangesAsync();

            // Начисляем XP
            int xpEarned = 0;
            double accuracy = (double)correctCount / totalCount;
            if (accuracy >= 0.5)
            {
                var rule = await _context.Rules.IgnoreQueryFilters().FirstOrDefaultAsync(r => r.Id == ruleId);
                int baseXp = rule?.XpReward ?? 50;
                xpEarned = (int)(baseXp * accuracy);

                // Бонус за повышение уровня
                if (progress.KnowledgeLevel > previousLevel)
                    xpEarned += 25;

                var stats = await _context.UserStats.FirstOrDefaultAsync(s => s.UserId == userId);
                if (stats != null)
                {
                    stats.TotalXp += xpEarned;
                    stats.LastUpdated = DateTime.UtcNow;
                }
            }

            await _context.SaveChangesAsync();

            // Инвалидируем кэш
            await _cache.TryRemoveAsync($"rules:{userId}");
            await _cache.TryRemoveAsync($"rules:available:{userId}");

            return Ok(new
            {
                RuleId = ruleId,
                CorrectAnswers = correctCount,
                TotalAnswers = totalCount,
                AccuracyPercent = Math.Round(accuracy * 100, 1),
                KnowledgeLevel = progress.KnowledgeLevel,
                PreviousLevel = previousLevel,
                LevelUp = progress.KnowledgeLevel > previousLevel,
                NextReview = progress.NextReview,
                XpEarned = xpEarned
            });
        }

        /// <summary>
        /// Получить прогресс по всей грамматике (для статистики).
        /// </summary>
        [HttpGet("progress-summary")]
        public async Task<IActionResult> GetProgressSummary()
        {
            var userId = GetUserId();

            var allProgress = await _context.GrammarProgresses
                .IgnoreQueryFilters()
                .Where(gp => gp.UserId == userId)
                .ToListAsync();

            var totalRules = await _context.Rules
                .IgnoreQueryFilters()
                .CountAsync(r => r.UserId == userId ||
                    _context.RuleSharings.Any(rs => rs.RuleId == r.Id && rs.StudentId == userId));

            var now = DateTime.UtcNow;

            return Ok(new
            {
                TotalRules = totalRules,
                StartedRules = allProgress.Count,
                MasteredRules = allProgress.Count(p => p.KnowledgeLevel >= 4),
                InProgressRules = allProgress.Count(p => p.KnowledgeLevel >= 1 && p.KnowledgeLevel < 4),
                DueForReview = allProgress.Count(p => p.NextReview != null && p.NextReview <= now),
                OverallAccuracy = allProgress.Sum(p => p.TotalAnswers) > 0
                    ? Math.Round((double)allProgress.Sum(p => p.CorrectAnswers) / allProgress.Sum(p => p.TotalAnswers) * 100, 1)
                    : 0.0,
                TotalSessions = allProgress.Sum(p => p.TotalSessions),
                HardestRules = allProgress
                    .Where(p => p.LapseCount > 0)
                    .OrderByDescending(p => p.LapseCount)
                    .Take(5)
                    .Select(p => new { p.RuleId, p.LapseCount, p.KnowledgeLevel })
                    .ToList()
            });
        }
    }

    // === DTOs ===

    public class GrammarSessionResult
    {
        public List<GrammarAnswerItem> Answers { get; set; } = new();
    }

    public class GrammarAnswerItem
    {
        public int ExerciseId { get; set; }
        public string? UserAnswer { get; set; }
        public bool IsCorrect { get; set; }
        public int? ResponseTimeMs { get; set; }
    }
}
