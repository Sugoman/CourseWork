using System.Net.Http.Json;

namespace LearningTrainerWeb.Services;

/// <summary>
/// Сервис для работы с публичным контентом (словари и правила)
/// </summary>
public interface IContentApiService
{
    // Dictionaries
    Task<PagedResult<DictionaryListItem>> GetPublicDictionariesAsync(
        string? search, string? languageFrom, string? languageTo, int page, int pageSize);
    Task<DictionaryDetailDto?> GetDictionaryDetailsAsync(int id);
    Task<List<CommentItem>> GetDictionaryCommentsAsync(int id);
    Task AddDictionaryCommentAsync(int dictionaryId, int rating, string text);
    Task DownloadDictionaryAsync(int dictionaryId);
    
    // Rules
    Task<PagedResult<RuleListItem>> GetPublicRulesAsync(
        string? search, string? category, int difficulty, int page, int pageSize);
    Task<RuleDetailDto?> GetRuleDetailsAsync(int id);
    Task<List<CommentItem>> GetRuleCommentsAsync(int id);
    Task AddRuleCommentAsync(int ruleId, int rating, string text);
    Task DownloadRuleAsync(int ruleId);
    Task<List<RuleListItem>> GetRelatedRulesAsync(int ruleId, string category);
    
    // My Content
    Task<List<MyDictionaryItem>> GetMyDictionariesAsync();
    Task<List<MyRuleItem>> GetMyRulesAsync();
    Task<List<DownloadedItem>> GetDownloadedContentAsync();
    Task PublishDictionaryAsync(int id);
    Task UnpublishDictionaryAsync(int id);
    Task PublishRuleAsync(int id);
    Task UnpublishRuleAsync(int id);
    
    // Token management
    void SetAuthToken(string? token);
}

public class ContentApiService : IContentApiService
{
    private readonly HttpClient _httpClient;

    public ContentApiService(HttpClient httpClient)
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

    #region Dictionaries

    public async Task<PagedResult<DictionaryListItem>> GetPublicDictionariesAsync(
        string? search, string? languageFrom, string? languageTo, int page, int pageSize)
    {
        var url = $"api/marketplace/dictionaries?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrEmpty(search)) url += $"&search={Uri.EscapeDataString(search)}";
        if (!string.IsNullOrEmpty(languageFrom)) url += $"&languageFrom={languageFrom}";
        if (!string.IsNullOrEmpty(languageTo)) url += $"&languageTo={languageTo}";

        var response = await _httpClient.GetFromJsonAsync<PagedResult<DictionaryListItem>>(url);
        return response ?? new PagedResult<DictionaryListItem>();
    }

    public async Task<DictionaryDetailDto?> GetDictionaryDetailsAsync(int id)
    {
        return await _httpClient.GetFromJsonAsync<DictionaryDetailDto>($"api/marketplace/dictionaries/{id}");
    }

    public async Task<List<CommentItem>> GetDictionaryCommentsAsync(int id)
    {
        var result = await _httpClient.GetFromJsonAsync<List<CommentItem>>($"api/marketplace/dictionaries/{id}/comments");
        return result ?? new List<CommentItem>();
    }

    public async Task AddDictionaryCommentAsync(int dictionaryId, int rating, string text)
    {
        var request = new { Rating = rating, Text = text };
        var response = await _httpClient.PostAsJsonAsync($"api/marketplace/dictionaries/{dictionaryId}/comments", request);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Не удалось добавить комментарий: {response.StatusCode}. {error}");
        }
    }

    public async Task DownloadDictionaryAsync(int dictionaryId)
    {
        var response = await _httpClient.PostAsync($"api/marketplace/dictionaries/{dictionaryId}/download", null);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Не удалось скачать словарь: {response.StatusCode}");
        }
    }

    #endregion

    #region Rules

    public async Task<PagedResult<RuleListItem>> GetPublicRulesAsync(
        string? search, string? category, int difficulty, int page, int pageSize)
    {
        var url = $"api/marketplace/rules?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrEmpty(search)) url += $"&search={Uri.EscapeDataString(search)}";
        if (!string.IsNullOrEmpty(category)) url += $"&category={category}";
        if (difficulty > 0) url += $"&difficulty={difficulty}";

        var response = await _httpClient.GetFromJsonAsync<PagedResult<RuleListItem>>(url);
        return response ?? new PagedResult<RuleListItem>();
    }

    public async Task<RuleDetailDto?> GetRuleDetailsAsync(int id)
    {
        return await _httpClient.GetFromJsonAsync<RuleDetailDto>($"api/marketplace/rules/{id}");
    }

    public async Task<List<CommentItem>> GetRuleCommentsAsync(int id)
    {
        var result = await _httpClient.GetFromJsonAsync<List<CommentItem>>($"api/marketplace/rules/{id}/comments");
        return result ?? new List<CommentItem>();
    }

    public async Task AddRuleCommentAsync(int ruleId, int rating, string text)
    {
        var request = new { Rating = rating, Text = text };
        var response = await _httpClient.PostAsJsonAsync($"api/marketplace/rules/{ruleId}/comments", request);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Не удалось добавить комментарий: {response.StatusCode}. {error}");
        }
    }

    public async Task DownloadRuleAsync(int ruleId)
    {
        var response = await _httpClient.PostAsync($"api/marketplace/rules/{ruleId}/download", null);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Не удалось скачать правило: {response.StatusCode}");
        }
    }

    public async Task<List<RuleListItem>> GetRelatedRulesAsync(int ruleId, string category)
    {
        var result = await _httpClient.GetFromJsonAsync<List<RuleListItem>>(
            $"api/marketplace/rules/{ruleId}/related?category={category}");
        return result ?? new List<RuleListItem>();
    }

    #endregion

    #region My Content

    public async Task<List<MyDictionaryItem>> GetMyDictionariesAsync()
    {
        var result = await _httpClient.GetFromJsonAsync<List<MyDictionaryItem>>("api/marketplace/my/dictionaries");
        return result ?? new List<MyDictionaryItem>();
    }

    public async Task<List<MyRuleItem>> GetMyRulesAsync()
    {
        var result = await _httpClient.GetFromJsonAsync<List<MyRuleItem>>("api/marketplace/my/rules");
        return result ?? new List<MyRuleItem>();
    }

    public async Task<List<DownloadedItem>> GetDownloadedContentAsync()
    {
        var result = await _httpClient.GetFromJsonAsync<List<DownloadedItem>>("api/marketplace/my/downloads");
        return result ?? new List<DownloadedItem>();
    }

    public async Task PublishDictionaryAsync(int id)
    {
        await _httpClient.PostAsync($"api/marketplace/dictionaries/{id}/publish", null);
    }

    public async Task UnpublishDictionaryAsync(int id)
    {
        await _httpClient.PostAsync($"api/marketplace/dictionaries/{id}/unpublish", null);
    }

    public async Task PublishRuleAsync(int id)
    {
        await _httpClient.PostAsync($"api/marketplace/rules/{id}/publish", null);
    }

    public async Task UnpublishRuleAsync(int id)
    {
        await _httpClient.PostAsync($"api/marketplace/rules/{id}/unpublish", null);
    }

    #endregion
}

#region DTOs

public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public int CurrentPage { get; set; }
}

public class DictionaryListItem
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string LanguageFrom { get; set; } = "";
    public string LanguageTo { get; set; } = "";
    public int WordCount { get; set; }
    public string AuthorName { get; set; } = "";
    public double Rating { get; set; }
    public int Downloads { get; set; }
}

public class DictionaryDetailDto : DictionaryListItem
{
    public int RatingCount { get; set; }
    public int AuthorContentCount { get; set; }
    public List<WordPreview> PreviewWords { get; set; } = new();
}

public class WordPreview
{
    public string Term { get; set; } = "";
    public string Translation { get; set; } = "";
}

public class RuleListItem
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Category { get; set; } = "";
    public int DifficultyLevel { get; set; }
    public string AuthorName { get; set; } = "";
    public double Rating { get; set; }
    public int Downloads { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class RuleDetailDto : RuleListItem
{
    public string HtmlContent { get; set; } = "";
    public int RatingCount { get; set; }
    public int AuthorContentCount { get; set; }
}

public class CommentItem
{
    public int Id { get; set; }
    public string AuthorName { get; set; } = "";
    public int Rating { get; set; }
    public string Text { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public class MyDictionaryItem
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int WordCount { get; set; }
    public bool IsPublished { get; set; }
    public double Rating { get; set; }
    public int Downloads { get; set; }
}

public class MyRuleItem
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Category { get; set; } = "";
    public bool IsPublished { get; set; }
    public double Rating { get; set; }
    public int Downloads { get; set; }
}

public class DownloadedItem
{
    public int Id { get; set; }
    public string Type { get; set; } = "";
    public string Title { get; set; } = "";
    public string AuthorName { get; set; } = "";
    public DateTime DownloadedAt { get; set; }
}

#endregion
