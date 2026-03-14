using LearningAPI.Extensions;
using LearningTrainerShared.Context;
using LearningTrainerShared.Models;
using LearningTrainerShared.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using StackExchange.Redis;
using System.Text.Json;

namespace LearningAPI.Controllers
{
    [ApiController]
    [Route("api/progress")]
    [Authorize]
    public class ProgressController : BaseApiController
    {
        private readonly ApiDbContext _context;
        private readonly ILogger<ProgressController> _logger;
        private readonly IDistributedCache _cache;
        private readonly ISpacedRepetitionService _spacedRepetition;
        private readonly IConnectionMultiplexer? _redis;

        public ProgressController(
            ApiDbContext context,
            ILogger<ProgressController> logger,
            IDistributedCache cache,
            ISpacedRepetitionService spacedRepetition,
            IConnectionMultiplexer? redis = null)
        {
            _context = context;
            _logger = logger;
            _cache = cache;
            _spacedRepetition = spacedRepetition;
            _redis = redis;
        }

        private async Task<TimeZoneInfo> GetUserTimeZoneAsync(int userId)
        {
            var tzId = await _context.Users
                .Where(u => u.Id == userId)
                .Select(u => u.TimeZoneId)
                .FirstOrDefaultAsync();

            if (string.IsNullOrEmpty(tzId))
                return TimeZoneInfo.Utc;

            try { return TimeZoneInfo.FindSystemTimeZoneById(tzId); }
            catch (TimeZoneNotFoundException) { return TimeZoneInfo.Utc; }
        }

        // POST /api/progress/update
        [HttpPost("update")]
        public async Task<IActionResult> UpdateProgress([FromBody] UpdateProgressRequest request, CancellationToken ct = default)
        {
            try
            {
                var userId = GetUserId();

                _logger.LogInformation("Updating progress for User={UserId}, Word={WordId}", userId, request.WordId);

                var wordExists = await _context.Words.AnyAsync(w => w.Id == request.WordId, ct);
                if (!wordExists)
                {
                    _logger.LogWarning("Word not found: {WordId}", request.WordId);

                    return NotFound(new { message = $"Слово с ID {request.WordId} не найдено." });
                }
                var progress = await _context.LearningProgresses
                    .FirstOrDefaultAsync(p => p.UserId == userId && p.WordId == request.WordId, ct);

                if (progress == null)
                {
                    progress = new LearningProgress
                    {
                        UserId = userId,
                        WordId = request.WordId,
                        KnowledgeLevel = 0,
                        NextReview = DateTime.UtcNow 
                    };
                    _context.LearningProgresses.Add(progress);
                    _logger.LogInformation("New progress created for User={UserId}, Word={WordId}", userId, request.WordId);
                }
                else
                {
                    _logger.LogInformation("Progress found for update: {@Progress}", progress);
                }

                _spacedRepetition.ApplyAnswer(progress, request.Quality, request.ResponseTimeMs);

                _logger.LogInformation("Progress updated to '{Quality}' for User={UserId}, Word={WordId}",
                    request.Quality, userId, request.WordId);

                // §5.1 LEARNING_IMPROVEMENTS: XP grant for correct answers
                if (request.Quality >= ResponseQuality.Hard)
                {
                    var xp = CalculateXp(request.Quality, request.ExerciseMode);
                    await GrantXpAsync(userId, xp, ct);
                }

                await _context.SaveChangesAsync(ct);
                _logger.LogInformation("Progress successfully updated for User={UserId}, Word={WordId}", userId, request.WordId);

                await InvalidateUserStatsCacheAsync(userId);

                return Ok(progress);
            }
            catch (DbUpdateException)
            {
                // Race condition: два параллельных запроса создали LearningProgress для одного WordId.
                // Повторяем запрос — теперь FindAsync найдёт существующую запись.
                _logger.LogWarning("Concurrency conflict for User={UserId}, Word={WordId}, retrying...", GetUserId(), request.WordId);

                try
                {
                    var userId = GetUserId();
                    var progress = await _context.LearningProgresses
                        .FirstOrDefaultAsync(p => p.UserId == userId && p.WordId == request.WordId, ct);

                    if (progress != null)
                    {
                        _spacedRepetition.ApplyAnswer(progress, request.Quality, request.ResponseTimeMs);
                        await _context.SaveChangesAsync(ct);
                        await InvalidateUserStatsCacheAsync(GetUserId());
                        return Ok(progress);
                    }
                }
                catch (Exception retryEx)
                {
                    _logger.LogError(retryEx, "Retry also failed for User={UserId}, Word={WordId}", GetUserId(), request.WordId);
                }

                return StatusCode(500, "Произошла ошибка при обновлении прогресса.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating progress for User={UserId}, Word={WordId}", GetUserId(), request.WordId);
                return StatusCode(500, "Произошла ошибка при обновлении прогресса.");
            }
        }

        // GET /api/progress/stats
        [HttpGet("stats")]
        public async Task<IActionResult> GetStats(CancellationToken ct = default)
        {
            var userId = GetUserId();

            var cacheKey = $"stats:{userId}";
            var cached = await _cache.TryGetStringAsync(cacheKey);
            if (cached != null)
            {
                return Content(cached, "application/json");
            }

            var progresses = _context.LearningProgresses
                .Where(p => p.UserId == userId);

            var stats = new DashboardStats();

            stats.LearnedWords = await progresses.CountAsync(p => p.KnowledgeLevel >= 4);

            var successSamples = await progresses
                .Where(p => p.TotalAttempts > 0)
                .Select(p => new { p.CorrectAnswers, p.TotalAttempts })
                .ToListAsync();

            stats.AverageSuccessRate = successSamples.Count == 0
                ? 0
                : successSamples.Average(s => (double)s.CorrectAnswers / s.TotalAttempts);

            var wordIds = await progresses
                .Select(p => p.WordId)
                .Distinct()
                .ToListAsync();

            stats.TotalWords = wordIds.Count;

            stats.TotalDictionaries = await _context.Words
                .Where(w => wordIds.Contains(w.Id))
                .Select(w => w.DictionaryId)
                .Distinct()
                .CountAsync();

            var userTz = await GetUserTimeZoneAsync(userId);
            var today = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, userTz).Date;
            var fromDate = today.AddDays(-6);

            // Calculate streak
            var practiceDates = await progresses
                .Where(p => p.LastPracticed != default)
                .Select(p => p.LastPracticed.Date)
                .Distinct()
                .OrderByDescending(d => d)
                .Take(400)
                .ToListAsync();

            var practiceSet = practiceDates.ToHashSet();
            var streak = 0;
            var checkDate = today;
            if (!practiceSet.Contains(today))
                checkDate = today.AddDays(-1);
            while (practiceSet.Contains(checkDate))
            {
                streak++;
                checkDate = checkDate.AddDays(-1);
            }
            stats.CurrentStreak = streak;

            var userStats = await _context.UserStats.FindAsync(userId);
            stats.BestStreak = userStats != null ? Math.Max(userStats.BestStreak, streak) : streak;

            // XP и уровни (§18.5a LEARNING_IMPROVEMENTS)
            if (userStats != null)
            {
                stats.TotalXp = userStats.TotalXp;
                stats.Level = userStats.Level;
                stats.XpForCurrentLevel = userStats.XpForCurrentLevel;
                stats.XpForNextLevel = userStats.XpForNextLevel;
            }

            stats.ActivityLast7Days = await progresses
                .Where(p => p.LastPracticed >= fromDate)
                .GroupBy(p => p.LastPracticed.Date)
                .Select(g => new ActivityPoint
                {
                    Date = g.Key,
                    Reviewed = g.Count(),
                    Learned = g.Count(p => p.KnowledgeLevel >= 4)
                })
                .ToListAsync();

            stats.KnowledgeDistribution = await progresses
                .GroupBy(p => p.KnowledgeLevel)
                .Select(g => new KnowledgeDistributionPoint
                {
                    Level = g.Key,
                    Count = g.Count()
                })
                .ToListAsync();

            var json = JsonSerializer.Serialize(stats);
            await _cache.TrySetStringAsync(cacheKey, json, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2)
            });

            return Ok(stats);
        }

        // PUT /api/progress/daily-goal
        /// <summary>
        /// Установить дневную цель (количество слов)
        /// </summary>
        [HttpPut("daily-goal")]
        public async Task<IActionResult> SetDailyGoal([FromBody] SetDailyGoalRequest request, CancellationToken ct = default)
        {
            if (request.Goal < 1 || request.Goal > 500)
                return BadRequest(new { message = "Дневная цель должна быть от 1 до 500 слов." });

            var userId = GetUserId();

            var userStats = await _context.UserStats.FindAsync(userId);
            if (userStats == null)
            {
                userStats = new UserStats { UserId = userId, DailyGoal = request.Goal };
                _context.UserStats.Add(userStats);
            }
            else
            {
                userStats.DailyGoal = request.Goal;
            }

            await _context.SaveChangesAsync(ct);
            await InvalidateUserStatsCacheAsync(userId);

            return Ok(new { dailyGoal = userStats.DailyGoal });
        }

        // POST /api/progress/unsuspend/{wordId}
        /// <summary>
        /// Снять заморозку (leech) со слова и вернуть его в очередь повторения
        /// </summary>
        [HttpPost("unsuspend/{wordId:int}")]
        public async Task<IActionResult> UnsuspendWord(int wordId, CancellationToken ct = default)
        {
            var userId = GetUserId();

            var progress = await _context.LearningProgresses
                .FirstOrDefaultAsync(p => p.UserId == userId && p.WordId == wordId, ct);

            if (progress == null)
                return NotFound(new { message = "Прогресс для этого слова не найден." });

            if (!progress.IsSuspended)
                return Ok(new { message = "Слово не заморожено." });

            progress.IsSuspended = false;
            progress.LapseCount = 0; // Сброс счётчика, чтобы дать новый шанс
            progress.NextReview = DateTime.UtcNow; // Поставить на немедленное повторение
            await _context.SaveChangesAsync(ct);
            await InvalidateUserStatsCacheAsync(userId);

            return Ok(new { message = "Слово разморожено и возвращено в очередь повторения." });
        }

        // DELETE /api/progress/{wordId}
        /// <summary>
        /// Полностью удалить прогресс слова (убрать из обучения)
        /// </summary>
        [HttpDelete("{wordId:int}")]
        public async Task<IActionResult> RemoveFromLearning(int wordId, CancellationToken ct = default)
        {
            var userId = GetUserId();

            var progress = await _context.LearningProgresses
                .FirstOrDefaultAsync(p => p.UserId == userId && p.WordId == wordId, ct);

            if (progress == null)
                return NotFound(new { message = "Прогресс для этого слова не найден." });

            _context.LearningProgresses.Remove(progress);
            await _context.SaveChangesAsync(ct);
            await InvalidateUserStatsCacheAsync(userId);

            return Ok(new { message = "Слово удалено из обучения." });
        }

        // GET /api/progress/leeches
        /// <summary>
        /// Получить список замороженных слов (leeches)
        /// </summary>
        [HttpGet("leeches")]
        public async Task<IActionResult> GetLeeches(CancellationToken ct = default)
        {
            var userId = GetUserId();

            var leeches = await _context.LearningProgresses
                .Where(p => p.UserId == userId && p.IsSuspended && p.Word != null)
                .Select(p => new TrainingWordDto
                {
                    WordId = p.WordId,
                    OriginalWord = p.Word!.OriginalWord,
                    Translation = p.Word.Translation,
                    Transcription = p.Word.Transcription,
                    Example = p.Word.Example,
                    DictionaryName = p.Word.Dictionary != null ? p.Word.Dictionary.Name : "",
                    DictionaryId = p.Word.DictionaryId,
                    DictionaryTags = p.Word.Dictionary != null ? p.Word.Dictionary.Tags : null,
                    KnowledgeLevel = p.KnowledgeLevel,
                    NextReview = p.NextReview,
                    TotalAttempts = p.TotalAttempts,
                    CorrectAnswers = p.CorrectAnswers,
                    LapseCount = p.LapseCount,
                    IsLeech = true,
                    UserNote = p.UserNote
                })
                .ToListAsync(ct);

            return Ok(leeches);
        }

        // PUT /api/progress/note/{wordId}
        /// <summary>
        /// Сохранить или обновить персональную заметку к слову (§8.3 LEARNING_IMPROVEMENTS)
        /// </summary>
        [HttpPut("note/{wordId:int}")]
        public async Task<IActionResult> SaveUserNote(int wordId, [FromBody] SaveNoteRequest request, CancellationToken ct = default)
        {
            var userId = GetUserId();

            var progress = await _context.LearningProgresses
                .FirstOrDefaultAsync(p => p.UserId == userId && p.WordId == wordId, ct);

            if (progress == null)
            {
                // Создаём запись прогресса, если её нет
                var wordExists = await _context.Words.AnyAsync(w => w.Id == wordId, ct);
                if (!wordExists)
                    return NotFound(new { message = "Слово не найдено." });

                progress = new LearningProgress
                {
                    UserId = userId,
                    WordId = wordId,
                    KnowledgeLevel = 0,
                    NextReview = DateTime.UtcNow,
                    UserNote = request.Note?.Trim()
                };
                _context.LearningProgresses.Add(progress);
            }
            else
            {
                progress.UserNote = request.Note?.Trim();
            }

            await _context.SaveChangesAsync(ct);
            return Ok(new { userNote = progress.UserNote });
        }

        // POST /api/progress/use-streak-freeze
        /// <summary>
        /// Использовать Streak Freeze для защиты серии (§19.9 LEARNING_IMPROVEMENTS).
        /// Стоимость: 100 XP. Можно использовать 1 раз в день.
        /// </summary>
        [HttpPost("use-streak-freeze")]
        public async Task<IActionResult> UseStreakFreeze(CancellationToken ct = default)
        {
            var userId = GetUserId();
            var userStats = await _context.UserStats.FindAsync(new object[] { userId }, ct);
            if (userStats == null)
                return BadRequest(new { message = "Статистика пользователя не найдена." });

            var userTz = await GetUserTimeZoneAsync(userId);
            var today = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, userTz).Date;

            // Check: already used today?
            if (userStats.LastFreezeUsedDate?.Date == today)
                return BadRequest(new { message = "Streak Freeze уже использован сегодня." });

            // Check: has available freezes?
            if (userStats.StreakFreezeCount <= 0)
            {
                // Can buy with XP (100 XP cost)
                const int freezeCost = 100;
                if (userStats.TotalXp < freezeCost)
                    return BadRequest(new { message = $"Недостаточно XP. Нужно {freezeCost} XP, у вас {userStats.TotalXp}." });

                userStats.TotalXp -= freezeCost;
            }
            else
            {
                userStats.StreakFreezeCount--;
            }

            // Apply freeze: set LastPracticeDate to today to preserve streak
            userStats.LastFreezeUsedDate = today;
            userStats.LastPracticeDate = today;
            userStats.LastUpdated = DateTime.UtcNow;

            await _context.SaveChangesAsync(ct);
            await InvalidateUserStatsCacheAsync(userId);

            return Ok(new
            {
                message = "Streak Freeze активирован! Серия сохранена.",
                streakFreezeCount = userStats.StreakFreezeCount,
                totalXp = userStats.TotalXp
            });
        }

        // GET /api/progress/streak-freeze-info
        /// <summary>
        /// Информация о streak freeze: доступные заморозки, стоимость, можно ли использовать.
        /// </summary>
        [HttpGet("streak-freeze-info")]
        public async Task<IActionResult> GetStreakFreezeInfo(CancellationToken ct = default)
        {
            var userId = GetUserId();
            var userStats = await _context.UserStats.FindAsync(new object[] { userId }, ct);
            if (userStats == null)
                return Ok(new { available = 0, canUse = false, canBuy = false, cost = 100, totalXp = 0L });

            var userTz = await GetUserTimeZoneAsync(userId);
            var today = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, userTz).Date;
            var usedToday = userStats.LastFreezeUsedDate?.Date == today;
            var yesterday = today.AddDays(-1);
            // Only show freeze option if user didn't practice yesterday (streak at risk)
            var needsFreeze = userStats.LastPracticeDate?.Date < yesterday;

            return Ok(new
            {
                available = userStats.StreakFreezeCount,
                canUse = !usedToday && needsFreeze && (userStats.StreakFreezeCount > 0 || userStats.TotalXp >= 100),
                canBuy = userStats.StreakFreezeCount <= 0 && userStats.TotalXp >= 100,
                cost = 100,
                totalXp = userStats.TotalXp,
                usedToday,
                needsFreeze
            });
        }

        private async Task InvalidateUserStatsCacheAsync(int userId)
        {
            await _cache.TryRemoveAsync($"stats:{userId}");
            await _cache.TryRemoveByPrefixAsync($"stats:full:{userId}:", _redis);
        }

        // === XP system (§5.1 LEARNING_IMPROVEMENTS) ===

        /// <summary>
        /// Рассчитать XP за ответ на основе качества и типа упражнения.
        /// </summary>
        private static int CalculateXp(ResponseQuality quality, string? exerciseMode)
        {
            var baseXp = exerciseMode?.ToLowerInvariant() switch
            {
                "typing" or "cloze" => 15,
                "listening" => 20,
                _ => 10 // flashcard, mcq, matching
            };

            return quality switch
            {
                ResponseQuality.Easy => (int)(baseXp * 1.5),
                ResponseQuality.Good => baseXp,
                ResponseQuality.Hard => (int)(baseXp * 0.5),
                _ => 0
            };
        }

        /// <summary>
        /// Начислить XP пользователю (с учётом streak-множителя).
        /// </summary>
        private async Task GrantXpAsync(int userId, int xp, CancellationToken ct)
        {
            if (xp <= 0) return;

            var userStats = await _context.UserStats.FindAsync(new object[] { userId }, ct);
            if (userStats == null)
            {
                userStats = new UserStats { UserId = userId, TotalXp = 0 };
                _context.UserStats.Add(userStats);
            }

            // Streak multiplier: +10% за каждый день подряд (макс ×2.0)
            var streakMultiplier = Math.Min(2.0, 1.0 + userStats.CurrentStreak * 0.1);
            var finalXp = (int)(xp * streakMultiplier);

            userStats.TotalXp += finalXp;
        }

        // GET /api/progress/xp
        /// <summary>
        /// Получить текущий XP и уровень пользователя.
        /// </summary>
        [HttpGet("xp")]
        public async Task<IActionResult> GetXp(CancellationToken ct = default)
        {
            var userId = GetUserId();
            var userStats = await _context.UserStats.FindAsync(new object[] { userId }, ct);

            var totalXp = userStats?.TotalXp ?? 0;
            var level = (int)Math.Floor(Math.Sqrt(totalXp / 50.0)) + 1;
            var xpForCurrentLevel = (long)((level - 1) * (level - 1)) * 50;
            var xpForNextLevel = (long)(level * level) * 50;

            return Ok(new
            {
                totalXp,
                level,
                xpForCurrentLevel,
                xpForNextLevel,
                xpInCurrentLevel = totalXp - xpForCurrentLevel,
                xpNeededForNext = xpForNextLevel - totalXp,
                streakMultiplier = userStats != null
                    ? Math.Min(2.0, 1.0 + userStats.CurrentStreak * 0.1)
                    : 1.0
            });
        }
    }
}
