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
    Task<List<TrainingWordDto>> GetTrainingWordsAsync(string mode = "mixed", int? dictionaryId = null, int limit = 20, string? tag = null);
    
    /// <summary>
    /// Обновить прогресс слова
    /// </summary>
    Task<bool> UpdateProgressAsync(int wordId, ResponseQuality quality, int? responseTimeMs = null, string? exerciseMode = null);
    
    /// <summary>
    /// Установить стартовый набор
    /// </summary>
    Task<StarterPackResult?> InstallStarterPackAsync();

    /// <summary>
    /// Получить список замороженных слов (leeches)
    /// </summary>
    Task<List<TrainingWordDto>> GetLeechesAsync();

    /// <summary>
    /// Снять заморозку (leech) со слова
    /// </summary>
    Task<bool> UnsuspendWordAsync(int wordId);

    /// <summary>
    /// Установить дневную цель
    /// </summary>
    Task<bool> SetDailyGoalAsync(int goal);

    /// <summary>
    /// Сохранить персональную заметку к слову
    /// </summary>
    Task<bool> SaveUserNoteAsync(int wordId, string? note);

    /// <summary>
    /// Получить ежедневный челлендж (§5.2 LEARNING_IMPROVEMENTS)
    /// </summary>
    Task<DailyChallengeDto?> GetDailyChallengeAsync();

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

    private async Task ApplyAuthAsync() => await _tokenProvider.EnsureValidTokenAsync(_httpClient);

    public void SetAuthToken(string? token)
    {
        // Token is now managed via AuthTokenProvider.ApplyTo().
    }

    public async Task<DailyPlanDto?> GetDailyPlanAsync(int newWordsLimit = 10, int reviewLimit = 20)
    {
        try
        {
            await ApplyAuthAsync();
            var url = $"api/training/daily-plan?newWordsLimit={newWordsLimit}&reviewLimit={reviewLimit}";
            return await _httpClient.GetFromJsonAsync<DailyPlanDto>(url);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching daily plan");
            return null;
        }
    }

    public async Task<List<TrainingWordDto>> GetTrainingWordsAsync(string mode = "mixed", int? dictionaryId = null, int limit = 20, string? tag = null)
    {
        try
        {
            await ApplyAuthAsync();
            var url = $"api/training/words?mode={mode}&limit={limit}";
            if (dictionaryId.HasValue)
            {
                url += $"&dictionaryId={dictionaryId.Value}";
            }
            if (!string.IsNullOrEmpty(tag))
            {
                url += $"&tag={Uri.EscapeDataString(tag)}";
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

    public async Task<bool> UpdateProgressAsync(int wordId, ResponseQuality quality, int? responseTimeMs = null, string? exerciseMode = null)
    {
        try
        {
            await ApplyAuthAsync();
            var request = new UpdateProgressRequest { WordId = wordId, Quality = quality, ResponseTimeMs = responseTimeMs, ExerciseMode = exerciseMode };
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
            await ApplyAuthAsync();
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

    public async Task<List<TrainingWordDto>> GetLeechesAsync()
    {
        try
        {
            await ApplyAuthAsync();
            var result = await _httpClient.GetFromJsonAsync<List<TrainingWordDto>>("api/progress/leeches");
            return result ?? new List<TrainingWordDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching leeches");
            return new List<TrainingWordDto>();
        }
    }

    public async Task<bool> UnsuspendWordAsync(int wordId)
    {
        try
        {
            await ApplyAuthAsync();
            var response = await _httpClient.PostAsync($"api/progress/unsuspend/{wordId}", null);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unsuspending word {WordId}", wordId);
            return false;
        }
    }

    public async Task<bool> SetDailyGoalAsync(int goal)
    {
        try
        {
            await ApplyAuthAsync();
            var request = new SetDailyGoalRequest { Goal = goal };
            var response = await _httpClient.PutAsJsonAsync("api/progress/daily-goal", request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting daily goal");
            return false;
        }
    }

    public async Task<bool> SaveUserNoteAsync(int wordId, string? note)
    {
        try
        {
            await ApplyAuthAsync();
            var request = new SaveNoteRequest { Note = note };
            var response = await _httpClient.PutAsJsonAsync($"api/progress/note/{wordId}", request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving note for word {WordId}", wordId);
            return false;
        }
    }

    public async Task<DailyChallengeDto?> GetDailyChallengeAsync()
    {
        try
        {
            await ApplyAuthAsync();
            return await _httpClient.GetFromJsonAsync<DailyChallengeDto>("api/training/daily-challenge");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching daily challenge");
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
