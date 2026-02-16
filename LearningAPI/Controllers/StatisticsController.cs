using LearningAPI.Extensions;
using LearningAPI.Services;
using LearningTrainerShared.Models.Statistics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using StackExchange.Redis;
using System.Text.Json;

namespace LearningAPI.Controllers;

[ApiController]
[Route("api/statistics")]
[Authorize]
public class StatisticsController : BaseApiController
{
    private readonly IStatisticsService _statisticsService;
    private readonly IDistributedCache _cache;
    private readonly ILogger<StatisticsController> _logger;
    private readonly IConnectionMultiplexer? _redis;

    public StatisticsController(
        IStatisticsService statisticsService,
        IDistributedCache cache,
        ILogger<StatisticsController> logger,
        IConnectionMultiplexer? redis = null)
    {
        _statisticsService = statisticsService;
        _cache = cache;
        _logger = logger;
        _redis = redis;
    }

    /// <summary>
    /// Получить полную статистику пользователя
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(UserStatistics), 200)]
    public async Task<IActionResult> GetFullStatistics(
        [FromQuery] string period = "week",
        CancellationToken ct = default)
    {
        var userId = GetUserId();
        var cacheKey = $"stats:full:{userId}:{period}";

        var cached = await _cache.TryGetStringAsync(cacheKey);
        if (!string.IsNullOrEmpty(cached))
        {
            return Content(cached, "application/json");
        }

        var stats = await _statisticsService.GetFullStatisticsAsync(userId, period, ct);

        var json = JsonSerializer.Serialize(stats);
        await _cache.TrySetStringAsync(cacheKey, json, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2)
        });

        return Ok(stats);
    }

    /// <summary>
    /// Получить краткую сводку для дашборда
    /// </summary>
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(CancellationToken ct = default)
    {
        var userId = GetUserId();
        var stats = await _statisticsService.GetFullStatisticsAsync(userId, "week", ct);

        var summary = new
        {
            stats.TotalWords,
            stats.LearnedWords,
            stats.CurrentStreak,
            stats.BestStreak,
            stats.OverallAccuracy,
            stats.WordsLearnedToday,
            stats.WordsLearnedThisWeek,
            stats.TotalCorrectAnswers,
            stats.TotalWrongAnswers,
            TimeSpentSeconds = (long)stats.TotalLearningTime.TotalSeconds
        };

        return Ok(summary);
    }

    /// <summary>
    /// Получить активность по дням
    /// </summary>
    [HttpGet("daily")]
    [ProducesResponseType(typeof(List<DailyActivityStats>), 200)]
    public async Task<IActionResult> GetDailyActivity(
        [FromQuery] int days = 30,
        CancellationToken ct = default)
    {
        var userId = GetUserId();
        days = Math.Clamp(days, 1, 365);
        
        var activity = await _statisticsService.GetDailyActivityAsync(userId, days, ct);
        return Ok(activity);
    }

    /// <summary>
    /// Получить статистику по словарям
    /// </summary>
    [HttpGet("dictionaries")]
    [ProducesResponseType(typeof(List<DictionaryStats>), 200)]
    public async Task<IActionResult> GetDictionaryStats(CancellationToken ct = default)
    {
        var userId = GetUserId();
        var stats = await _statisticsService.GetDictionaryStatsAsync(userId, ct);
        return Ok(stats);
    }

    /// <summary>
    /// Получить топ сложных слов
    /// </summary>
    [HttpGet("difficult-words")]
    [ProducesResponseType(typeof(List<DifficultWord>), 200)]
    public async Task<IActionResult> GetDifficultWords(
        [FromQuery] int limit = 20,
        CancellationToken ct = default)
    {
        var userId = GetUserId();
        limit = Math.Clamp(limit, 1, 100);
        
        var words = await _statisticsService.GetDifficultWordsAsync(userId, limit, ct);
        return Ok(words);
    }

    /// <summary>
    /// Получить все достижения пользователя
    /// </summary>
    [HttpGet("achievements")]
    [ProducesResponseType(typeof(List<Achievement>), 200)]
    public async Task<IActionResult> GetAchievements(CancellationToken ct = default)
    {
        var userId = GetUserId();
        var achievements = await _statisticsService.GetAchievementsAsync(userId, ct);
        return Ok(achievements);
    }

    /// <summary>
    /// Сохранить результат тренировочной сессии
    /// </summary>
    [HttpPost("session")]
    public async Task<IActionResult> SaveSession(
        [FromBody] SaveSessionRequest request,
        CancellationToken ct = default)
    {
        var userId = GetUserId();

        if (request.CompletedAt <= request.StartedAt)
        {
            return BadRequest("CompletedAt must be after StartedAt");
        }

        await _statisticsService.SaveSessionAsync(userId, request, ct);

        // Инвалидируем кеш
        await InvalidateUserStatsCacheAsync(userId);

        return Ok(new { message = "Session saved successfully" });
    }

    /// <summary>
    /// Обновить агрегированную статистику пользователя
    /// </summary>
    [HttpPost("refresh")]
    public async Task<IActionResult> RefreshStats(CancellationToken ct = default)
    {
        var userId = GetUserId();
        await _statisticsService.UpdateUserStatsAsync(userId, ct);

        // Инвалидируем кеш
        await InvalidateUserStatsCacheAsync(userId);

        return Ok(new { message = "Statistics refreshed" });
    }

    private async Task InvalidateUserStatsCacheAsync(int userId)
    {
        await _cache.TryRemoveAsync($"stats:{userId}");
        await _cache.TryRemoveByPrefixAsync($"stats:full:{userId}:", _redis);
    }
}
