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
    void SetAuthToken(string? token);
}

public class TrainingApiService : ITrainingApiService
{
    private readonly HttpClient _httpClient;

    public TrainingApiService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public void SetAuthToken(string? token)
    {
        if (string.IsNullOrEmpty(token))
        {
            _httpClient.DefaultRequestHeaders.Authorization = null;
        }
        else
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }
    }

    public async Task<DailyPlanDto?> GetDailyPlanAsync(int newWordsLimit = 10, int reviewLimit = 20)
    {
        try
        {
            var url = $"api/training/daily-plan?newWordsLimit={newWordsLimit}&reviewLimit={reviewLimit}";
            return await _httpClient.GetFromJsonAsync<DailyPlanDto>(url);
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    public async Task<List<TrainingWordDto>> GetTrainingWordsAsync(string mode = "mixed", int? dictionaryId = null, int limit = 20)
    {
        try
        {
            var url = $"api/training/words?mode={mode}&limit={limit}";
            if (dictionaryId.HasValue)
            {
                url += $"&dictionaryId={dictionaryId.Value}";
            }
            
            var result = await _httpClient.GetFromJsonAsync<List<TrainingWordDto>>(url);
            return result ?? new List<TrainingWordDto>();
        }
        catch (HttpRequestException)
        {
            return new List<TrainingWordDto>();
        }
    }

    public async Task<bool> UpdateProgressAsync(int wordId, ResponseQuality quality)
    {
        try
        {
            var request = new UpdateProgressRequest { WordId = wordId, Quality = quality };
            var response = await _httpClient.PostAsJsonAsync("api/progress/update", request);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }

    public async Task<StarterPackResult?> InstallStarterPackAsync()
    {
        try
        {
            var response = await _httpClient.PostAsync("api/training/starter-pack", null);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<StarterPackResult>();
            }
            return null;
        }
        catch (HttpRequestException)
        {
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
