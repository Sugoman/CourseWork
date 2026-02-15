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

    // Export
    Task<ExportResult> ExportDictionaryAsJsonAsync(int dictionaryId);
    Task<ExportResult> ExportDictionaryAsCsvAsync(int dictionaryId);
    Task<ExportResult> ExportAllDictionariesAsZipAsync();

    // Token management
    [Obsolete("Token is now managed automatically via AuthTokenDelegatingHandler")]
    void SetAuthToken(string? token);
}

public class ContentApiService : IContentApiService
{
    private readonly HttpClient _httpClient;
    private readonly AuthTokenProvider _tokenProvider;

    public ContentApiService(HttpClient httpClient, AuthTokenProvider tokenProvider)
    {
        _httpClient = httpClient;
        _tokenProvider = tokenProvider;
    }

    private void ApplyAuth() => _tokenProvider.ApplyTo(_httpClient);

    public void SetAuthToken(string? token)
    {
        // Token is now managed via AuthTokenProvider.ApplyTo().
    }

    #region Dictionaries

    public async Task<PagedResult<DictionaryListItem>> GetPublicDictionariesAsync(
        string? search, string? languageFrom, string? languageTo, int page, int pageSize)
    {
        var url = $"api/marketplace/dictionaries?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrEmpty(search)) url += $"&search={Uri.EscapeDataString(search)}";
        if (!string.IsNullOrEmpty(languageFrom)) url += $"&languageFrom={languageFrom}";
        if (!string.IsNullOrEmpty(languageTo)) url += $"&languageTo={languageTo}";

        ApplyAuth();
        var response = await _httpClient.GetFromJsonAsync<PagedResult<DictionaryListItem>>(url);
        return response ?? new PagedResult<DictionaryListItem>();
    }

    public async Task<DictionaryDetailDto?> GetDictionaryDetailsAsync(int id)
    {
        ApplyAuth();
        return await _httpClient.GetFromJsonAsync<DictionaryDetailDto>($"api/marketplace/dictionaries/{id}");
    }

    public async Task<List<CommentItem>> GetDictionaryCommentsAsync(int id)
    {
        ApplyAuth();
        var result = await _httpClient.GetFromJsonAsync<List<CommentItem>>($"api/marketplace/dictionaries/{id}/comments");
        return result ?? new List<CommentItem>();
    }

    public async Task AddDictionaryCommentAsync(int dictionaryId, int rating, string text)
    {
        ApplyAuth();
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
        ApplyAuth();
        var response = await _httpClient.PostAsync($"api/marketplace/dictionaries/{dictionaryId}/download", null);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            var message = TryExtractMessage(error) ?? $"Не удалось скачать словарь: {response.StatusCode}";
            throw new InvalidOperationException(message);
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

        ApplyAuth();
        var response = await _httpClient.GetFromJsonAsync<PagedResult<RuleListItem>>(url);
        return response ?? new PagedResult<RuleListItem>();
    }

    public async Task<RuleDetailDto?> GetRuleDetailsAsync(int id)
    {
        ApplyAuth();
        return await _httpClient.GetFromJsonAsync<RuleDetailDto>($"api/marketplace/rules/{id}");
    }

    public async Task<List<CommentItem>> GetRuleCommentsAsync(int id)
    {
        ApplyAuth();
        var result = await _httpClient.GetFromJsonAsync<List<CommentItem>>($"api/marketplace/rules/{id}/comments");
        return result ?? new List<CommentItem>();
    }

    public async Task AddRuleCommentAsync(int ruleId, int rating, string text)
    {
        ApplyAuth();
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
        ApplyAuth();
        var response = await _httpClient.PostAsync($"api/marketplace/rules/{ruleId}/download", null);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            var message = TryExtractMessage(error) ?? $"Не удалось скачать правило: {response.StatusCode}";
            throw new InvalidOperationException(message);
        }
    }

    public async Task<List<RuleListItem>> GetRelatedRulesAsync(int ruleId, string category)
    {
        ApplyAuth();
        var result = await _httpClient.GetFromJsonAsync<List<RuleListItem>>(
            $"api/marketplace/rules/{ruleId}/related?category={category}");
        return result ?? new List<RuleListItem>();
    }

    #endregion

    #region My Content

    public async Task<List<MyDictionaryItem>> GetMyDictionariesAsync()
    {
        ApplyAuth();
        var result = await _httpClient.GetFromJsonAsync<List<MyDictionaryItem>>("api/marketplace/my/dictionaries");
        return result ?? new List<MyDictionaryItem>();
    }

    public async Task<List<MyRuleItem>> GetMyRulesAsync()
    {
        ApplyAuth();
        var result = await _httpClient.GetFromJsonAsync<List<MyRuleItem>>("api/marketplace/my/rules");
        return result ?? new List<MyRuleItem>();
    }

    public async Task<List<DownloadedItem>> GetDownloadedContentAsync()
    {
        ApplyAuth();
        var result = await _httpClient.GetFromJsonAsync<List<DownloadedItem>>("api/marketplace/my/downloads");
        return result ?? new List<DownloadedItem>();
    }

    public async Task PublishDictionaryAsync(int id)
    {
        ApplyAuth();
        await _httpClient.PostAsync($"api/marketplace/dictionaries/{id}/publish", null);
    }

    public async Task UnpublishDictionaryAsync(int id)
    {
        ApplyAuth();
        await _httpClient.PostAsync($"api/marketplace/dictionaries/{id}/unpublish", null);
    }

    public async Task PublishRuleAsync(int id)
    {
        ApplyAuth();
        await _httpClient.PostAsync($"api/marketplace/rules/{id}/publish", null);
    }

    public async Task UnpublishRuleAsync(int id)
    {
        ApplyAuth();
        await _httpClient.PostAsync($"api/marketplace/rules/{id}/unpublish", null);
    }

    #endregion

    #region Export

    public async Task<ExportResult> ExportDictionaryAsJsonAsync(int dictionaryId)
    {
        ApplyAuth();
        var response = await _httpClient.GetAsync($"api/dictionaries/export/{dictionaryId}/json");
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsByteArrayAsync();
        var fileName = GetFileNameFromResponse(response) ?? $"dictionary_{dictionaryId}.json";

        return new ExportResult { Data = content, FileName = fileName, ContentType = "application/json" };
    }

    public async Task<ExportResult> ExportDictionaryAsCsvAsync(int dictionaryId)
    {
        ApplyAuth();
        var response = await _httpClient.GetAsync($"api/dictionaries/export/{dictionaryId}/csv");
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsByteArrayAsync();
        var fileName = GetFileNameFromResponse(response) ?? $"dictionary_{dictionaryId}.csv";

        return new ExportResult { Data = content, FileName = fileName, ContentType = "text/csv" };
    }

    public async Task<ExportResult> ExportAllDictionariesAsZipAsync()
    {
        ApplyAuth();
        var response = await _httpClient.GetAsync("api/dictionaries/export/all/zip");
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsByteArrayAsync();
        var fileName = GetFileNameFromResponse(response) ?? "dictionaries_export.zip";

        return new ExportResult { Data = content, FileName = fileName, ContentType = "application/zip" };
    }

    private static string? GetFileNameFromResponse(HttpResponseMessage response)
    {
        if (response.Content.Headers.ContentDisposition?.FileName != null)
        {
            return response.Content.Headers.ContentDisposition.FileName.Trim('"');
        }
        return null;
    }

    /// <summary>
    /// Извлекает Message из JSON-ответа API ({"Message":"..."}) или возвращает null.
    /// </summary>
    private static string? TryExtractMessage(string? responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody)) return null;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(responseBody);
            if (doc.RootElement.TryGetProperty("Message", out var msg) ||
                doc.RootElement.TryGetProperty("message", out msg))
            {
                return msg.GetString();
            }
        }
        catch { }
        return null;
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

public class ExportResult
{
    public byte[] Data { get; set; } = Array.Empty<byte>();
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
}

#endregion
