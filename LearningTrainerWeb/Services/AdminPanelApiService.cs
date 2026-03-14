using System.Net.Http.Json;

namespace LearningTrainerWeb.Services;

public interface IAdminPanelApiService
{
    Task<List<AdminUserInfo>?> SearchUsersAsync(string query);
    Task<AdminUserStats?> GetUserStatsAsync(int userId);
    Task<AddXpResult?> AddXpAsync(int userId, long amount);
    Task<BoostWordsResult?> BoostWordsAsync(int userId, int count);
    Task<GrantAchievementResult?> GrantAchievementAsync(int userId, string achievementId);
    Task<GrantAllResult?> GrantAllAchievementsAsync(int userId);
}

public class AdminPanelApiService : IAdminPanelApiService
{
    private readonly HttpClient _httpClient;
    private readonly AuthTokenProvider _tokenProvider;
    private readonly ILogger<AdminPanelApiService> _logger;

    public AdminPanelApiService(HttpClient httpClient, AuthTokenProvider tokenProvider, ILogger<AdminPanelApiService> logger)
    {
        _httpClient = httpClient;
        _tokenProvider = tokenProvider;
        _logger = logger;
    }

    private async Task ApplyAuthAsync()
        => await _tokenProvider.EnsureValidTokenAsync(_httpClient);

    public async Task<List<AdminUserInfo>?> SearchUsersAsync(string query)
    {
        try
        {
            await ApplyAuthAsync();
            var response = await _httpClient.GetAsync($"api/admin/panel/users/search?query={Uri.EscapeDataString(query)}");
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<List<AdminUserInfo>>();
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching users");
            return null;
        }
    }

    public async Task<AdminUserStats?> GetUserStatsAsync(int userId)
    {
        try
        {
            await ApplyAuthAsync();
            var response = await _httpClient.GetAsync($"api/admin/panel/users/{userId}/stats");
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<AdminUserStats>();
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user stats");
            return null;
        }
    }

    public async Task<AddXpResult?> AddXpAsync(int userId, long amount)
    {
        try
        {
            await ApplyAuthAsync();
            var response = await _httpClient.PostAsJsonAsync($"api/admin/panel/users/{userId}/add-xp", new { Amount = amount });
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<AddXpResult>();
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding XP");
            return null;
        }
    }

    public async Task<BoostWordsResult?> BoostWordsAsync(int userId, int count)
    {
        try
        {
            await ApplyAuthAsync();
            var response = await _httpClient.PostAsJsonAsync($"api/admin/panel/users/{userId}/boost-words", new { Count = count });
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<BoostWordsResult>();
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error boosting words");
            return null;
        }
    }

    public async Task<GrantAchievementResult?> GrantAchievementAsync(int userId, string achievementId)
    {
        try
        {
            await ApplyAuthAsync();
            var response = await _httpClient.PostAsJsonAsync(
                $"api/admin/panel/users/{userId}/grant-achievement",
                new { AchievementId = achievementId });
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<GrantAchievementResult>();
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error granting achievement");
            return null;
        }
    }

    public async Task<GrantAllResult?> GrantAllAchievementsAsync(int userId)
    {
        try
        {
            await ApplyAuthAsync();
            var response = await _httpClient.PostAsJsonAsync($"api/admin/panel/users/{userId}/grant-all-achievements", new { });
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<GrantAllResult>();
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error granting all achievements");
            return null;
        }
    }
}

// DTOs
public class AdminUserInfo
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string Email { get; set; } = "";
    public string Role { get; set; } = "";
}

public class AdminUserStats
{
    public AdminUserInfo User { get; set; } = new();
    public long TotalXp { get; set; }
    public int Level { get; set; }
    public int CurrentStreak { get; set; }
    public int LearnedWords { get; set; }
    public int TotalWords { get; set; }
    public List<AdminAchievementInfo> Achievements { get; set; } = [];
}

public class AdminAchievementInfo
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Icon { get; set; } = "";
    public string Category { get; set; } = "";
    public string Rarity { get; set; } = "";
    public int TargetValue { get; set; }
    public bool IsUnlocked { get; set; }
    public DateTime? UnlockedAt { get; set; }
    public int? CurrentProgress { get; set; }
}

public class AddXpResult
{
    public long TotalXp { get; set; }
    public int Level { get; set; }
}

public class BoostWordsResult
{
    public int BoostedCount { get; set; }
    public int TotalLearnedWords { get; set; }
}

public class GrantAchievementResult
{
    public string Message { get; set; } = "";
    public string Title { get; set; } = "";
    public string Icon { get; set; } = "";
}

public class GrantAllResult
{
    public int GrantedCount { get; set; }
}
