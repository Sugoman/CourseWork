using LearningTrainerShared.Models.Statistics;
using System.Net.Http.Json;
using System.Net.Http.Headers;

namespace LearningTrainerWeb.Services;

public class StatisticsApiService
{
    private readonly HttpClient _httpClient;
    private readonly IAuthService _authService;
    private readonly ILogger<StatisticsApiService> _logger;

    public StatisticsApiService(
        HttpClient httpClient,
        IAuthService authService,
        ILogger<StatisticsApiService> logger)
    {
        _httpClient = httpClient;
        _authService = authService;
        _logger = logger;
    }

    private void AddAuthHeader()
    {
        var token = _authService.GetToken();
        if (!string.IsNullOrEmpty(token))
        {
            _httpClient.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue("Bearer", token);
        }
    }

    public async Task<UserStatistics?> GetStatisticsAsync(string period = "week")
    {
        try
        {
            AddAuthHeader();
            var response = await _httpClient.GetAsync($"api/statistics?period={period}");
            
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<UserStatistics>();
            }
            
            _logger.LogWarning("Failed to get statistics: {StatusCode}", response.StatusCode);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching statistics");
            return null;
        }
    }

    public async Task<StatisticsSummary?> GetSummaryAsync()
    {
        try
        {
            AddAuthHeader();
            var response = await _httpClient.GetAsync("api/statistics/summary");

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<StatisticsSummary>();
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching statistics summary");
            return null;
        }
    }

    public async Task<List<DailyActivityStats>> GetDailyActivityAsync(int days = 30)
    {
        try
        {
            AddAuthHeader();
            var response = await _httpClient.GetAsync($"api/statistics/daily?days={days}");

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<List<DailyActivityStats>>() 
                    ?? new List<DailyActivityStats>();
            }

            return new List<DailyActivityStats>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching daily activity");
            return new List<DailyActivityStats>();
        }
    }

    public async Task<List<DictionaryStats>> GetDictionaryStatsAsync()
    {
        try
        {
            AddAuthHeader();
            var response = await _httpClient.GetAsync("api/statistics/dictionaries");

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<List<DictionaryStats>>() 
                    ?? new List<DictionaryStats>();
            }

            return new List<DictionaryStats>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching dictionary stats");
            return new List<DictionaryStats>();
        }
    }

    public async Task<List<DifficultWord>> GetDifficultWordsAsync(int limit = 20)
    {
        try
        {
            AddAuthHeader();
            var response = await _httpClient.GetAsync($"api/statistics/difficult-words?limit={limit}");

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<List<DifficultWord>>() 
                    ?? new List<DifficultWord>();
            }

            return new List<DifficultWord>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching difficult words");
            return new List<DifficultWord>();
        }
    }

    public async Task<List<Achievement>> GetAchievementsAsync()
    {
        try
        {
            AddAuthHeader();
            var response = await _httpClient.GetAsync("api/statistics/achievements");

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<List<Achievement>>() 
                    ?? new List<Achievement>();
            }

            return new List<Achievement>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching achievements");
            return new List<Achievement>();
        }
    }

    public async Task<bool> SaveSessionAsync(SaveSessionRequest request)
    {
        try
        {
            AddAuthHeader();
            var response = await _httpClient.PostAsJsonAsync("api/statistics/session", request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving session");
            return false;
        }
    }
}

public class SaveSessionRequest
{
    public DateTime StartedAt { get; set; }
    public DateTime CompletedAt { get; set; }
    public int WordsReviewed { get; set; }
    public int CorrectAnswers { get; set; }
    public int WrongAnswers { get; set; }
    public string Mode { get; set; } = "Flashcards";
    public int? DictionaryId { get; set; }
}

public class StatisticsSummary
{
    public int TotalWords { get; set; }
    public int LearnedWords { get; set; }
    public int CurrentStreak { get; set; }
    public int BestStreak { get; set; }
    public double OverallAccuracy { get; set; }
    public int WordsLearnedToday { get; set; }
    public int WordsLearnedThisWeek { get; set; }
}
