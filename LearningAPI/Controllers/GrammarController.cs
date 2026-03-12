using LearningAPI.Extensions;
using LearningAPI.Services;
using LearningTrainerShared.Context;
using LearningTrainerShared.Models;
using LearningTrainerShared.Models.Features.Ai;
using LearningTrainerShared.Services;
using Markdig;
using Ganss.Xss;
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
        private readonly IAiGrammarExerciseService _aiGrammarExerciseService;
        private readonly ILogger<GrammarController> _logger;

        private static readonly HashSet<string> SupportedExerciseTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "mcq",
            "transformation",
            "error_correction",
            "word_order",
            "translation",
            "matching",
            "dictation"
        };

        private static readonly MarkdownPipeline _markdownPipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .UseEmojiAndSmiley()
            .Build();

        private static readonly HtmlSanitizer _htmlSanitizer = LearningTrainerShared.Services.SharedSanitizerFactory.Create();

        public GrammarController(
            ApiDbContext context,
            ISpacedRepetitionService srs,
            IDistributedCache cache,
            IAiGrammarExerciseService aiGrammarExerciseService,
            ILogger<GrammarController> logger)
        {
            _context = context;
            _srs = srs;
            _cache = cache;
            _aiGrammarExerciseService = aiGrammarExerciseService;
            _logger = logger;
        }

        private static string ConvertMarkdownToHtml(string? markdown)
        {
            if (string.IsNullOrEmpty(markdown)) return "";
            var html = Markdown.ToHtml(markdown, _markdownPipeline);
            return _htmlSanitizer.Sanitize(html);
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
        /// Получить детали правила: теория (HTML), прогресс, метаданные.
        /// Для страницы навыка с вкладками (§17.5.2).
        /// </summary>
        [HttpGet("{ruleId:int}/details")]
        public async Task<IActionResult> GetRuleDetail(int ruleId)
        {
            var userId = GetUserId();

            var sharedRuleIds = await _context.RuleSharings
                .Where(rs => rs.StudentId == userId)
                .Select(rs => rs.RuleId)
                .ToListAsync();

            var rule = await _context.Rules
                .IgnoreQueryFilters()
                .Include(r => r.Exercises)
                .FirstOrDefaultAsync(r => r.Id == ruleId &&
                    (r.UserId == userId || sharedRuleIds.Contains(r.Id)));

            if (rule == null)
                return NotFound("Правило не найдено");

            var progress = await _context.GrammarProgresses
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(gp => gp.UserId == userId && gp.RuleId == ruleId);

            return Ok(new
            {
                rule.Id,
                rule.Title,
                rule.Description,
                rule.Category,
                rule.DifficultyLevel,
                rule.SkillTreeLevel,
                rule.IconEmoji,
                rule.SkillSummary,
                rule.XpReward,
                HtmlContent = ConvertMarkdownToHtml(rule.MarkdownContent),
                ExerciseCount = rule.Exercises.Count,
                // Progress
                KnowledgeLevel = progress?.KnowledgeLevel ?? 0,
                EaseFactor = progress?.EaseFactor ?? 2.5,
                IntervalDays = progress?.IntervalDays ?? 0,
                NextReview = progress?.NextReview,
                TotalSessions = progress?.TotalSessions ?? 0,
                CorrectAnswers = progress?.CorrectAnswers ?? 0,
                TotalAnswers = progress?.TotalAnswers ?? 0,
                AccuracyPercent = (progress?.TotalAnswers ?? 0) > 0
                    ? Math.Round((double)(progress!.CorrectAnswers) / progress.TotalAnswers * 100, 1)
                    : 0.0,
                LastPracticeDate = progress?.LastPracticeDate,
                LapseCount = progress?.LapseCount ?? 0
            });
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
        /// Сгенерировать упражнения через AI и сохранить их в банк GrammarExercise (§17.9.8).
        /// </summary>
        [HttpPost("{ruleId:int}/generate-exercises")]
        public async Task<IActionResult> GenerateExercises(
            int ruleId,
            [FromQuery(Name = "type")] string exerciseType = "mcq",
            [FromQuery] int count = 5,
            [FromQuery] int difficultyTier = 1)
        {
            var userId = GetUserId();

            exerciseType = (exerciseType ?? "mcq").Trim().ToLowerInvariant();

            if (!SupportedExerciseTypes.Contains(exerciseType))
                return BadRequest($"Неподдерживаемый type: {exerciseType}");

            if (count is < 1 or > 20)
                return BadRequest("count должен быть в диапазоне 1..20");

            if (difficultyTier is < 1 or > 3)
                return BadRequest("difficultyTier должен быть в диапазоне 1..3");

            var rule = await _context.Rules
                .IgnoreQueryFilters()
                .Include(r => r.Exercises)
                .FirstOrDefaultAsync(r => r.Id == ruleId &&
                    (r.UserId == userId || _context.RuleSharings.Any(rs => rs.RuleId == r.Id && rs.StudentId == userId)));

            if (rule == null)
                return NotFound("Правило не найдено");

            // POST-эндпоинт всегда генерирует новые упражнения через AI.
            // Существующие упражнения возвращаются только как fallback при недоступности AI.
            var existing = rule.Exercises
                .Where(e => e.ExerciseType == exerciseType && e.DifficultyTier == difficultyTier)
                .OrderByDescending(e => e.Id)
                .Take(count)
                .OrderBy(e => e.OrderIndex)
                .ToList();

            var generated = new List<GrammarExercise>();
            bool aiGenerationAttempted = true;
            bool aiGenerationSucceeded = false;
            bool aiServiceUnavailable = false;
            string? warning = null;

            {
                var aiResult = await _aiGrammarExerciseService.GenerateTypedExercisesAsync(
                    rule.Title,
                    rule.MarkdownContent,
                    exerciseType,
                    count,
                    difficultyTier);

                aiGenerationSucceeded = aiResult.IsSuccess;
                aiServiceUnavailable = aiResult.IsServiceUnavailable;
                var aiResults = aiResult.Exercises;

                if (aiResults.Count == 0)
                {
                    _logger.LogWarning(
                        "AI returned no exercises for rule {RuleId} and type {ExerciseType}. Success={IsSuccess}, ServiceUnavailable={IsServiceUnavailable}, Error={Error}",
                        ruleId,
                        exerciseType,
                        aiResult.IsSuccess,
                        aiResult.IsServiceUnavailable,
                        aiResult.ErrorMessage);

                    warning = aiResult.IsServiceUnavailable
                        ? "AI service is unavailable; returned exercises from existing bank only."
                        : "AI returned no new exercises.";
                }

                int orderSeed = rule.Exercises.Any() ? rule.Exercises.Max(e => e.OrderIndex) + 1 : 0;

                generated = aiResults
                    .Select((item, index) => ToGrammarExerciseEntity(ruleId, exerciseType, difficultyTier, item, orderSeed + index))
                    .ToList();

                if (generated.Count > 0)
                {
                    _context.GrammarExercises.AddRange(generated);
                    await _context.SaveChangesAsync();
                }
            }

            if (existing.Count == 0 && generated.Count == 0)
            {
                if (aiGenerationAttempted && aiServiceUnavailable)
                {
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                    {
                        RuleId = ruleId,
                        RuleTitle = rule.Title,
                        ExerciseType = exerciseType,
                        DifficultyTier = difficultyTier,
                        RequestedCount = count,
                        Message = "AI service is unavailable and no cached exercises exist for this rule/type.",
                        AiGenerationAttempted = true,
                        AiGenerationSucceeded = false,
                        AiServiceUnavailable = true,
                        Warning = warning,
                        Exercises = Array.Empty<object>()
                    });
                }

                return Ok(new GeneratedGrammarExercisesResponse
                {
                    RuleId = ruleId,
                    RuleTitle = rule.Title,
                    ExerciseType = exerciseType,
                    DifficultyTier = difficultyTier,
                    RequestedCount = count,
                    GeneratedCount = 0,
                    ReturnedCount = 0,
                    AiGenerationAttempted = aiGenerationAttempted,
                    AiGenerationSucceeded = aiGenerationSucceeded,
                    AiServiceUnavailable = aiServiceUnavailable,
                    Warning = warning ?? "No exercises available for this rule/type.",
                    Exercises = new List<GeneratedGrammarExerciseDto>()
                });
            }

            // Если AI вернул упражнения — возвращаем только новые.
            // Если AI недоступен — fallback на existing из банка.
            var exercisesToReturn = generated.Count > 0
                ? generated
                : existing;

            var response = new GeneratedGrammarExercisesResponse
            {
                RuleId = ruleId,
                RuleTitle = rule.Title,
                ExerciseType = exerciseType,
                DifficultyTier = difficultyTier,
                RequestedCount = count,
                GeneratedCount = generated.Count,
                ReturnedCount = exercisesToReturn.Count,
                AiGenerationAttempted = aiGenerationAttempted,
                AiGenerationSucceeded = aiGenerationSucceeded,
                AiServiceUnavailable = aiServiceUnavailable,
                Warning = warning,
                Exercises = exercisesToReturn
                    .Take(count)
                    .Select(ToGeneratedExerciseDto)
                    .ToList()
            };

            // Инвалидация списков правил на случай, если клиент отображает количество упражнений.
            if (generated.Count > 0)
            {
                await _cache.TryRemoveAsync($"rules:{userId}");
                await _cache.TryRemoveAsync($"rules:available:{userId}");
            }

            return Ok(response);
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

        private static GrammarExercise ToGrammarExerciseEntity(
            int ruleId,
            string exerciseType,
            int difficultyTier,
            AiTypedExerciseResult item,
            int orderIndex)
        {
            var options = item.Options?.Where(o => !string.IsNullOrWhiteSpace(o)).ToArray() ?? Array.Empty<string>();
            var alternativeAnswers = item.AlternativeAnswers?.Where(a => !string.IsNullOrWhiteSpace(a)).ToArray() ?? Array.Empty<string>();
            var shuffledWords = item.ShuffledWords?.Where(w => !string.IsNullOrWhiteSpace(w)).ToArray() ?? Array.Empty<string>();

            var correctIndex = item.CorrectIndex ?? 0;
            if (correctIndex < 0 || correctIndex >= options.Length)
                correctIndex = 0;

            var question = string.IsNullOrWhiteSpace(item.Question)
                ? (item.IncorrectSentence ?? "Complete the exercise")
                : item.Question;

            return new GrammarExercise
            {
                RuleId = ruleId,
                ExerciseType = exerciseType,
                Question = question,
                Options = options,
                CorrectIndex = correctIndex,
                CorrectAnswer = item.CorrectAnswer,
                AlternativeAnswers = alternativeAnswers,
                IncorrectSentence = item.IncorrectSentence,
                ShuffledWords = shuffledWords,
                Explanation = item.Explanation ?? string.Empty,
                DifficultyTier = difficultyTier,
                OrderIndex = orderIndex
            };
        }

        private static GeneratedGrammarExerciseDto ToGeneratedExerciseDto(GrammarExercise exercise)
        {
            return new GeneratedGrammarExerciseDto
            {
                Id = exercise.Id,
                ExerciseType = exercise.ExerciseType,
                Question = exercise.Question,
                Options = exercise.Options,
                CorrectIndex = exercise.CorrectIndex,
                CorrectAnswer = exercise.CorrectAnswer,
                AlternativeAnswers = exercise.AlternativeAnswers,
                IncorrectSentence = exercise.IncorrectSentence,
                ShuffledWords = exercise.ShuffledWords,
                Explanation = exercise.Explanation,
                DifficultyTier = exercise.DifficultyTier,
                OrderIndex = exercise.OrderIndex
            };
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

    public class GeneratedGrammarExercisesResponse
    {
        public int RuleId { get; set; }
        public string RuleTitle { get; set; } = "";
        public string ExerciseType { get; set; } = "mcq";
        public int DifficultyTier { get; set; }
        public int RequestedCount { get; set; }
        public int GeneratedCount { get; set; }
        public int ReturnedCount { get; set; }
        public bool AiGenerationAttempted { get; set; }
        public bool AiGenerationSucceeded { get; set; }
        public bool AiServiceUnavailable { get; set; }
        public string? Warning { get; set; }
        public List<GeneratedGrammarExerciseDto> Exercises { get; set; } = new();
    }

    public class GeneratedGrammarExerciseDto
    {
        public int Id { get; set; }
        public string ExerciseType { get; set; } = "mcq";
        public string Question { get; set; } = "";
        public string[] Options { get; set; } = Array.Empty<string>();
        public int CorrectIndex { get; set; }
        public string? CorrectAnswer { get; set; }
        public string[] AlternativeAnswers { get; set; } = Array.Empty<string>();
        public string? IncorrectSentence { get; set; }
        public string[] ShuffledWords { get; set; } = Array.Empty<string>();
        public string Explanation { get; set; } = "";
        public int DifficultyTier { get; set; }
        public int OrderIndex { get; set; }
    }
}
