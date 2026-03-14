using LearningTrainerShared.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LearningAPI.Controllers;

/// <summary>
/// Контроллер профилей пользователей.
/// Требует аутентификации для предотвращения перечисления (IDOR).
/// </summary>
[ApiController]
[Route("api/users")]
[Authorize]
public class UserProfileController : BaseApiController
{
    private readonly ApiDbContext _context;

    public UserProfileController(ApiDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Получить профиль пользователя (требуется аутентификация)
    /// </summary>
    [HttpGet("{id}/profile")]
    public async Task<IActionResult> GetPublicProfile(int id)
    {
        var user = await _context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null)
            return NotFound();

        var stats = await _context.UserStats
            .FirstOrDefaultAsync(s => s.UserId == id);

        var achievements = await _context.UserAchievements
            .Where(a => a.UserId == id)
            .Select(a => a.AchievementId)
            .ToListAsync();

        var publishedDictionaries = await _context.Dictionaries
            .IgnoreQueryFilters()
            .Where(d => d.UserId == id && d.IsPublished)
            .Select(d => new PublishedContentItemDto
            {
                Id = d.Id,
                Name = d.Name,
                Rating = d.Rating,
                DownloadCount = d.DownloadCount
            })
            .ToListAsync();

        var publishedRules = await _context.Rules
            .IgnoreQueryFilters()
            .Where(r => r.UserId == id && r.IsPublished)
            .Select(r => new PublishedContentItemDto
            {
                Id = r.Id,
                Name = r.Title,
                Rating = r.Rating,
                DownloadCount = r.DownloadCount
            })
            .ToListAsync();

        var profile = new UserPublicProfileDto
        {
            Id = user.Id,
            Username = user.Username,
            MemberSince = user.CreatedAt,
            CurrentStreak = stats?.CurrentStreak ?? 0,
            BestStreak = stats?.BestStreak ?? 0,
            PublishedDictionariesCount = publishedDictionaries.Count,
            PublishedRulesCount = publishedRules.Count,
            Achievements = achievements,
            PublishedDictionaries = publishedDictionaries,
            PublishedRules = publishedRules
        };

        // Роль видна только самому пользователю и администрaторам
        var requesterId = GetUserId();
        var requesterRole = GetUserRole();
        if (requesterId == id || requesterRole == "Admin")
        {
            profile.Role = user.Role?.Name ?? "User";
            profile.TotalSessions = stats?.TotalSessions ?? 0;
        }

        return Ok(profile);
    }
}

#region DTOs

public class UserPublicProfileDto
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string? Role { get; set; }
    public DateTime MemberSince { get; set; }
    public int CurrentStreak { get; set; }
    public int BestStreak { get; set; }
    public int? TotalSessions { get; set; }
    public int PublishedDictionariesCount { get; set; }
    public int PublishedRulesCount { get; set; }
    public List<string> Achievements { get; set; } = new();
    public List<PublishedContentItemDto> PublishedDictionaries { get; set; } = new();
    public List<PublishedContentItemDto> PublishedRules { get; set; } = new();
}

public class PublishedContentItemDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public double Rating { get; set; }
    public int DownloadCount { get; set; }
}

#endregion
