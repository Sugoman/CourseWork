using System.Net.Http.Json;
using LearningTrainerShared.Models;

namespace LearningTrainerWeb.Services;

/// <summary>
/// Сервис для работы с тренировками
/// </summary>
public interface ITrainingApiService
{
    /// <summary>
    /// Получить план тренировки на сегодня
    /// </summary>
    Task<DailyPlanDto?> GetDailyPlanAsync(int newWordsLimit = 10, int reviewLimit = 20);
    
    /// <summary>
    /// Получить слова для тренировки
    /// </summary>
    Task<List<TrainingWordDto>> GetTrainingWordsAsync(string mode = "mixed", int? dictionaryId = null, int limit = 20);
    
    /// <summary>
    /// Обновить прогресс слова
    /// </summary>
    Task<bool> UpdateProgressAsync(int wordId, ResponseQuality quality);
    
    /// <summary>
    /// Установить стартовый набор
    /// </summary>
    Task<StarterPackResult?> InstallStarterPackAsync();
    
    /// <summary>
    /// Установить токен авторизации
    /// </summary>
    [Obsolete("Token is now managed automatically via AuthTokenDelegatingHandler")]
    void SetAuthToken(string? token);
}

public class TrainingApiService : ITrainingApiService
{
    private readonly HttpClient _httpClient;
    private readonly AuthTokenProvider _tokenProvider;
    private readonly ILogger<TrainingApiService> _logger;

    public TrainingApiService(HttpClient httpClient, AuthTokenProvider tokenProvider, ILogger<TrainingApiService> logger)
    {
        _httpClient = httpClient;
        _tokenProvider = tokenProvider;
        _logger = logger;
    }

    private void ApplyAuth() => _tokenProvider.ApplyTo(_httpClient);

    public void SetAuthToken(string? token)
    {
        // Token is now managed via AuthTokenProvider.ApplyTo().
    }

    public async Task<DailyPlanDto?> GetDailyPlanAsync(int newWordsLimit = 10, int reviewLimit = 20)
    {
        try
        {
            ApplyAuth();
            var url = $"api/training/daily-plan?newWordsLimit={newWordsLimit}&reviewLimit={reviewLimit}";
            return await _httpClient.GetFromJsonAsync<DailyPlanDto>(url);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching daily plan");
            return null;
        }
    }

    public async Task<List<TrainingWordDto>> GetTrainingWordsAsync(string mode = "mixed", int? dictionaryId = null, int limit = 20)
    {
        try
        {
            ApplyAuth();
            var url = $"api/training/words?mode={mode}&limit={limit}";
            if (dictionaryId.HasValue)
            {
                url += $"&dictionaryId={dictionaryId.Value}";
            }

            var result = await _httpClient.GetFromJsonAsync<List<TrainingWordDto>>(url);
            return result ?? new List<TrainingWordDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching training words");
            return new List<TrainingWordDto>();
        }
    }

    public async Task<bool> UpdateProgressAsync(int wordId, ResponseQuality quality)
    {
        try
        {
            ApplyAuth();
            var request = new UpdateProgressRequest { WordId = wordId, Quality = quality };
            var response = await _httpClient.PostAsJsonAsync("api/progress/update", request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating progress for word {WordId}", wordId);
            return false;
        }
    }

    public async Task<StarterPackResult?> InstallStarterPackAsync()
    {
        try
        {
            ApplyAuth();
            var response = await _httpClient.PostAsync("api/training/starter-pack", null);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<StarterPackResult>();
            }
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error installing starter pack");
            return null;
        }
    }
}

#region DTOs

public class StarterPackResult
{
    public string Message { get; set; } = "";
    public int DictionaryId { get; set; }
    public int WordCount { get; set; }
}

#endregion
