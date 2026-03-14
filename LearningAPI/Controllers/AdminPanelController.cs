using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using LearningTrainerShared.Context;
using LearningTrainerShared.Models;
using LearningTrainerShared.Models.Statistics;
using LearningTrainerShared.Constants;

namespace LearningAPI.Controllers;

[ApiController]
[Route("api/admin/panel")]
[Authorize(Roles = UserRoles.Admin)]
public class AdminPanelController : BaseApiController
{
    private readonly ApiDbContext _context;
    private readonly ILogger<AdminPanelController> _logger;

    public AdminPanelController(ApiDbContext context, ILogger<AdminPanelController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Поиск пользователей по имени или email (автокомплит)
    /// </summary>
    [HttpGet("users/search")]
    public async Task<IActionResult> SearchUsers([FromQuery] string query)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
            return Ok(Array.Empty<object>());

        var users = await _context.Users
            .Include(u => u.Role)
            .Where(u => u.Username.Contains(query) || u.Email.Contains(query))
            .Take(20)
            .Select(u => new
            {
                u.Id,
                u.Username,
                u.Email,
                Role = u.Role.Name
            })
            .ToListAsync();

        return Ok(users);
    }

    /// <summary>
    /// Получить текущие данные пользователя (XP, слова, достижения)
    /// </summary>
    [HttpGet("users/{userId}/stats")]
    public async Task<IActionResult> GetUserStats(int userId)
    {
        var user = await _context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
            return NotFound(new { message = "Пользователь не найден" });

        var stats = await _context.UserStats.FirstOrDefaultAsync(s => s.UserId == userId);

        var learnedWordsCount = await _context.LearningProgresses
            .CountAsync(lp => lp.UserId == userId && lp.KnowledgeLevel >= 4);

        var totalWordsCount = await _context.LearningProgresses
            .CountAsync(lp => lp.UserId == userId);

        var achievements = await _context.UserAchievements
            .Where(a => a.UserId == userId)
            .Select(a => new
            {
                a.AchievementId,
                a.UnlockedAt,
                a.CurrentProgress
            })
            .ToListAsync();

        // Составляем список всех достижений с отметками о разблокировке
        var allAchievements = AchievementDefinitions.All.Select(def =>
        {
            var userAch = achievements.FirstOrDefault(a => a.AchievementId == def.Id);
            return new
            {
                def.Id,
                def.Title,
                def.Icon,
                def.Category,
                def.Rarity,
                def.TargetValue,
                IsUnlocked = userAch != null,
                UnlockedAt = userAch?.UnlockedAt,
                CurrentProgress = userAch?.CurrentProgress
            };
        }).ToList();

        return Ok(new
        {
            User = new { user.Id, user.Username, user.Email, Role = user.Role.Name },
            TotalXp = stats?.TotalXp ?? 0,
            Level = stats?.Level ?? 1,
            CurrentStreak = stats?.CurrentStreak ?? 0,
            LearnedWords = learnedWordsCount,
            TotalWords = totalWordsCount,
            Achievements = allAchievements
        });
    }

    /// <summary>
    /// Накрутить XP пользователю
    /// </summary>
    [HttpPost("users/{userId}/add-xp")]
    public async Task<IActionResult> AddXp(int userId, [FromBody] AddXpRequest request)
    {
        if (request.Amount <= 0)
            return BadRequest(new { message = "Количество XP должно быть положительным" });

        var stats = await _context.UserStats.FirstOrDefaultAsync(s => s.UserId == userId);
        if (stats == null)
        {
            // Создаём запись если её нет
            var userExists = await _context.Users.AnyAsync(u => u.Id == userId);
            if (!userExists)
                return NotFound(new { message = "Пользователь не найден" });

            stats = new UserStats { UserId = userId, TotalXp = 0 };
            _context.UserStats.Add(stats);
        }

        stats.TotalXp += request.Amount;
        stats.LastUpdated = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Admin {AdminId} added {Amount} XP to user {UserId}. New total: {TotalXp}",
            GetUserId(), request.Amount, userId, stats.TotalXp);

        return Ok(new { stats.TotalXp, stats.Level });
    }

    /// <summary>
    /// Накрутить выученные слова — поднять KnowledgeLevel до 5 для N случайных слов
    /// </summary>
    [HttpPost("users/{userId}/boost-words")]
    public async Task<IActionResult> BoostWords(int userId, [FromBody] BoostWordsRequest request)
    {
        if (request.Count <= 0)
            return BadRequest(new { message = "Количество слов должно быть положительным" });

        var userExists = await _context.Users.AnyAsync(u => u.Id == userId);
        if (!userExists)
            return NotFound(new { message = "Пользователь не найден" });

        // Находим слова пользователя с KnowledgeLevel < 4
        var unlearnedProgress = await _context.LearningProgresses
            .Where(lp => lp.UserId == userId && lp.KnowledgeLevel < 4)
            .Take(request.Count)
            .ToListAsync();

        var boosted = 0;

        foreach (var lp in unlearnedProgress)
        {
            lp.KnowledgeLevel = 5;
            lp.CorrectAnswers = Math.Max(lp.CorrectAnswers, 10);
            lp.EaseFactor = 2.5;
            lp.IntervalDays = 30;
            lp.LastPracticed = DateTime.UtcNow;
            lp.NextReview = DateTime.UtcNow.AddDays(30);
            boosted++;
        }

        // Если не хватает существующих прогрессов, создаём для слов из словарей пользователя
        if (boosted < request.Count)
        {
            var existingWordIds = await _context.LearningProgresses
                .Where(lp => lp.UserId == userId)
                .Select(lp => lp.WordId)
                .ToListAsync();

            var userDictIds = await _context.Dictionaries
                .Where(d => d.UserId == userId)
                .Select(d => d.Id)
                .ToListAsync();

            var newWordIds = await _context.Words
                .Where(w => userDictIds.Contains(w.DictionaryId) && !existingWordIds.Contains(w.Id))
                .Select(w => w.Id)
                .Take(request.Count - boosted)
                .ToListAsync();

            foreach (var wordId in newWordIds)
            {
                _context.LearningProgresses.Add(new LearningProgress
                {
                    UserId = userId,
                    WordId = wordId,
                    KnowledgeLevel = 5,
                    CorrectAnswers = 10,
                    TotalAttempts = 10,
                    EaseFactor = 2.5,
                    IntervalDays = 30,
                    LastPracticed = DateTime.UtcNow,
                    NextReview = DateTime.UtcNow.AddDays(30)
                });
                boosted++;
            }
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Admin {AdminId} boosted {Count} words for user {UserId}",
            GetUserId(), boosted, userId);

        var total = await _context.LearningProgresses
            .CountAsync(lp => lp.UserId == userId && lp.KnowledgeLevel >= 4);

        return Ok(new { BoostedCount = boosted, TotalLearnedWords = total });
    }

    /// <summary>
    /// Выдать достижение пользователю
    /// </summary>
    [HttpPost("users/{userId}/grant-achievement")]
    public async Task<IActionResult> GrantAchievement(int userId, [FromBody] GrantAchievementRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.AchievementId))
            return BadRequest(new { message = "AchievementId обязателен" });

        var definition = AchievementDefinitions.GetById(request.AchievementId);
        if (definition == null)
            return BadRequest(new { message = $"Достижение '{request.AchievementId}' не найдено" });

        var userExists = await _context.Users.AnyAsync(u => u.Id == userId);
        if (!userExists)
            return NotFound(new { message = "Пользователь не найден" });

        var existing = await _context.UserAchievements
            .FirstOrDefaultAsync(a => a.UserId == userId && a.AchievementId == request.AchievementId);

        if (existing != null)
            return Conflict(new { message = $"Достижение '{definition.Title}' уже разблокировано" });

        _context.UserAchievements.Add(new UserAchievement
        {
            UserId = userId,
            AchievementId = request.AchievementId,
            UnlockedAt = DateTime.UtcNow,
            CurrentProgress = definition.TargetValue
        });

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Admin {AdminId} granted achievement '{AchId}' to user {UserId}",
            GetUserId(), request.AchievementId, userId);

        return Ok(new { message = $"Достижение '{definition.Title}' выдано", definition.Title, definition.Icon });
    }

    /// <summary>
    /// Выдать все достижения пользователю
    /// </summary>
    [HttpPost("users/{userId}/grant-all-achievements")]
    public async Task<IActionResult> GrantAllAchievements(int userId)
    {
        var userExists = await _context.Users.AnyAsync(u => u.Id == userId);
        if (!userExists)
            return NotFound(new { message = "Пользователь не найден" });

        var existingIds = await _context.UserAchievements
            .Where(a => a.UserId == userId)
            .Select(a => a.AchievementId)
            .ToListAsync();

        var toGrant = AchievementDefinitions.All
            .Where(d => !existingIds.Contains(d.Id))
            .ToList();

        foreach (var def in toGrant)
        {
            _context.UserAchievements.Add(new UserAchievement
            {
                UserId = userId,
                AchievementId = def.Id,
                UnlockedAt = DateTime.UtcNow,
                CurrentProgress = def.TargetValue
            });
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Admin {AdminId} granted {Count} achievements to user {UserId}",
            GetUserId(), toGrant.Count, userId);

        return Ok(new { GrantedCount = toGrant.Count });
    }
}

// === Request DTOs ===

public record AddXpRequest
{
    public long Amount { get; init; }
}

public record BoostWordsRequest
{
    public int Count { get; init; }
}

public record GrantAchievementRequest
{
    public string AchievementId { get; init; } = string.Empty;
}
