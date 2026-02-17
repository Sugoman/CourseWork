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

        // POST /api/progress/update
        [HttpPost("update")]
        public async Task<IActionResult> UpdateProgress([FromBody] UpdateProgressRequest request, CancellationToken ct = default)
        {
            try
            {
                var userId = GetUserId();

                _logger.LogInformation("Updating progress for User={UserId}, Word={WordId}", userId, request.WordId);

                var wordExists = await _context.Words.AnyAsync(w => w.Id == request.WordId);
                if (!wordExists)
                {
                    _logger.LogWarning("Word not found: {WordId}", request.WordId);

                    return NotFound(new { message = $"Слово с ID {request.WordId} не найдено." });
                }
                var progress = await _context.LearningProgresses
                    .FirstOrDefaultAsync(p => p.UserId == userId && p.WordId == request.WordId);

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

                _spacedRepetition.ApplyAnswer(progress, request.Quality);

                _logger.LogInformation("Progress updated to '{Quality}' for User={UserId}, Word={WordId}",
                    request.Quality, userId, request.WordId);

                await _context.SaveChangesAsync();
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
                        .FirstOrDefaultAsync(p => p.UserId == userId && p.WordId == request.WordId);

                    if (progress != null)
                    {
                        _spacedRepetition.ApplyAnswer(progress, request.Quality);
                        await _context.SaveChangesAsync();
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

            var today = DateTime.UtcNow.Date;
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

        private async Task InvalidateUserStatsCacheAsync(int userId)
        {
            await _cache.TryRemoveAsync($"stats:{userId}");
            await _cache.TryRemoveByPrefixAsync($"stats:full:{userId}:", _redis);
        }
    }
}
