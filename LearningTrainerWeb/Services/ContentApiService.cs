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
    Task<PagedResult<CommentItem>> GetDictionaryCommentsAsync(int id, int page = 1, int pageSize = 5);
    Task AddDictionaryCommentAsync(int dictionaryId, int rating, string text);
    Task DownloadDictionaryAsync(int dictionaryId);

    // Rules
    Task<PagedResult<RuleListItem>> GetPublicRulesAsync(
        string? search, string? category, int difficulty, int page, int pageSize);
    Task<RuleDetailDto?> GetRuleDetailsAsync(int id);
    Task<PagedResult<CommentItem>> GetRuleCommentsAsync(int id, int page = 1, int pageSize = 5);
    Task AddRuleCommentAsync(int ruleId, int rating, string text);
    Task DownloadRuleAsync(int ruleId);
    Task<List<RuleListItem>> GetRelatedRulesAsync(int ruleId, string category);

    // My Content
    Task<List<MyDictionaryItem>> GetMyDictionariesAsync();
    Task<MyDictionaryFullDto?> GetMyDictionaryFullAsync(int id);
    Task<List<MyRuleItem>> GetMyRulesAsync();
    Task<MyRuleDetailItem?> GetMyRuleDetailsAsync(int id);
    Task<List<DownloadedItem>> GetDownloadedContentAsync();
    Task PublishDictionaryAsync(int id);
    Task UnpublishDictionaryAsync(int id);
    Task PublishRuleAsync(int id);
    Task UnpublishRuleAsync(int id);
    Task<List<string>> GetDictionaryTagsAsync();

    // Import
    Task<ImportResult> ImportDictionaryFromFileAsync(Stream fileStream, string fileName,
        string? dictionaryName = null, string? languageFrom = null, string? languageTo = null);
    Task<ImportResult> ImportDictionaryFromAnkiAsync(Stream fileStream, string fileName,
        string? dictionaryName = null, string? languageFrom = null, string? languageTo = null);

    // Create
    Task<CreatedDictionaryResult> CreateDictionaryAsync(string name, string description,
        string languageFrom, string languageTo, string? tags = null);
    Task<CreatedWordResult> AddWordAsync(int dictionaryId, string originalWord, string translation, string? example = null);
    Task<CreatedRuleResult> CreateRuleAsync(string title, string markdownContent, string description,
        string category, int difficultyLevel, List<CreateExerciseInput>? exercises = null);
    Task UpdateRuleAsync(int id, string title, string markdownContent, string description,
        string category, int difficultyLevel, List<CreateExerciseInput>? exercises = null);
    Task DeleteRuleAsync(int id);

    // Export
    Task<ExportResult> ExportDictionaryAsJsonAsync(int dictionaryId);
    Task<ExportResult> ExportDictionaryAsCsvAsync(int dictionaryId);
    Task<ExportResult> ExportAllDictionariesAsZipAsync();

    // Token management
    [Obsolete("Token is now managed automatically via AuthTokenDelegatingHandler")]
    void SetAuthToken(string? token);

    // Platform stats
    Task<PlatformStatsResponse> GetPlatformStatsAsync();

    // User profiles
    Task<UserPublicProfile?> GetUserProfileAsync(int userId);
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

    private async Task ApplyAuthAsync() => await _tokenProvider.EnsureValidTokenAsync(_httpClient);

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

        await ApplyAuthAsync();
        var response = await _httpClient.GetFromJsonAsync<PagedResult<DictionaryListItem>>(url);
        return response ?? new PagedResult<DictionaryListItem>();
    }

    public async Task<DictionaryDetailDto?> GetDictionaryDetailsAsync(int id)
    {
        await ApplyAuthAsync();
        return await _httpClient.GetFromJsonAsync<DictionaryDetailDto>($"api/marketplace/dictionaries/{id}");
    }

    public async Task<PagedResult<CommentItem>> GetDictionaryCommentsAsync(int id, int page = 1, int pageSize = 5)
    {
        await ApplyAuthAsync();
        var result = await _httpClient.GetFromJsonAsync<PagedResult<CommentItem>>($"api/marketplace/dictionaries/{id}/comments?page={page}&pageSize={pageSize}");
        return result ?? new PagedResult<CommentItem>();
    }

    public async Task AddDictionaryCommentAsync(int dictionaryId, int rating, string text)
    {
        await ApplyAuthAsync();
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
        await ApplyAuthAsync();
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

        await ApplyAuthAsync();
        var response = await _httpClient.GetFromJsonAsync<PagedResult<RuleListItem>>(url);
        return response ?? new PagedResult<RuleListItem>();
    }

    public async Task<RuleDetailDto?> GetRuleDetailsAsync(int id)
    {
        await ApplyAuthAsync();
        return await _httpClient.GetFromJsonAsync<RuleDetailDto>($"api/marketplace/rules/{id}");
    }

    public async Task<PagedResult<CommentItem>> GetRuleCommentsAsync(int id, int page = 1, int pageSize = 5)
    {
        await ApplyAuthAsync();
        var result = await _httpClient.GetFromJsonAsync<PagedResult<CommentItem>>($"api/marketplace/rules/{id}/comments?page={page}&pageSize={pageSize}");
        return result ?? new PagedResult<CommentItem>();
    }

    public async Task AddRuleCommentAsync(int ruleId, int rating, string text)
    {
        await ApplyAuthAsync();
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
        await ApplyAuthAsync();
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
        await ApplyAuthAsync();
        var result = await _httpClient.GetFromJsonAsync<List<RuleListItem>>(
            $"api/marketplace/rules/{ruleId}/related?category={category}");
        return result ?? new List<RuleListItem>();
    }

    #endregion

    #region My Content

    public async Task<List<MyDictionaryItem>> GetMyDictionariesAsync()
    {
        await ApplyAuthAsync();
        var result = await _httpClient.GetFromJsonAsync<List<MyDictionaryItem>>("api/marketplace/my/dictionaries");
        return result ?? new List<MyDictionaryItem>();
    }

    public async Task<MyDictionaryFullDto?> GetMyDictionaryFullAsync(int id)
    {
        await ApplyAuthAsync();
        return await _httpClient.GetFromJsonAsync<MyDictionaryFullDto>($"api/dictionaries/{id}");
    }

    public async Task<List<MyRuleItem>> GetMyRulesAsync()
    {
        await ApplyAuthAsync();
        var result = await _httpClient.GetFromJsonAsync<List<MyRuleItem>>("api/marketplace/my/rules");
        return result ?? new List<MyRuleItem>();
    }

    public async Task<MyRuleDetailItem?> GetMyRuleDetailsAsync(int id)
    {
        await ApplyAuthAsync();
        return await _httpClient.GetFromJsonAsync<MyRuleDetailItem>($"api/marketplace/my/rules/{id}");
    }

    public async Task<List<DownloadedItem>> GetDownloadedContentAsync()
    {
        await ApplyAuthAsync();
        var result = await _httpClient.GetFromJsonAsync<List<DownloadedItem>>("api/marketplace/my/downloads");
        return result ?? new List<DownloadedItem>();
    }

    public async Task PublishDictionaryAsync(int id)
    {
        await ApplyAuthAsync();
        await _httpClient.PostAsync($"api/marketplace/dictionaries/{id}/publish", null);
    }

    public async Task UnpublishDictionaryAsync(int id)
    {
        await ApplyAuthAsync();
        await _httpClient.PostAsync($"api/marketplace/dictionaries/{id}/unpublish", null);
    }

    public async Task PublishRuleAsync(int id)
    {
        await ApplyAuthAsync();
        await _httpClient.PostAsync($"api/marketplace/rules/{id}/publish", null);
    }

    public async Task UnpublishRuleAsync(int id)
    {
        await ApplyAuthAsync();
        await _httpClient.PostAsync($"api/marketplace/rules/{id}/unpublish", null);
    }

    public async Task<List<string>> GetDictionaryTagsAsync()
    {
        try
        {
            await ApplyAuthAsync();
            var result = await _httpClient.GetFromJsonAsync<List<string>>("api/dictionaries/tags");
            return result ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    #endregion

    #region Import

    public async Task<ImportResult> ImportDictionaryFromFileAsync(Stream fileStream, string fileName,
        string? dictionaryName = null, string? languageFrom = null, string? languageTo = null)
    {
        await ApplyAuthAsync();

        using var formContent = new MultipartFormDataContent();
        var streamContent = new StreamContent(fileStream);
        streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        formContent.Add(streamContent, "file", fileName);

        if (!string.IsNullOrEmpty(dictionaryName))
            formContent.Add(new StringContent(dictionaryName), "dictionaryName");
        if (!string.IsNullOrEmpty(languageFrom))
            formContent.Add(new StringContent(languageFrom), "languageFrom");
        if (!string.IsNullOrEmpty(languageTo))
            formContent.Add(new StringContent(languageTo), "languageTo");

        var response = await _httpClient.PostAsync("api/dictionaries/import/json/auto", formContent);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            var msg = TryExtractMessage(body) ?? body;
            throw new HttpRequestException(msg);
        }

        var result = await response.Content.ReadFromJsonAsync<ImportResult>();
        return result ?? new ImportResult();
    }

    public async Task<ImportResult> ImportDictionaryFromAnkiAsync(Stream fileStream, string fileName,
        string? dictionaryName = null, string? languageFrom = null, string? languageTo = null)
    {
        await ApplyAuthAsync();

        using var formContent = new MultipartFormDataContent();
        var streamContent = new StreamContent(fileStream);
        streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
        formContent.Add(streamContent, "file", fileName);

        if (!string.IsNullOrEmpty(dictionaryName))
            formContent.Add(new StringContent(dictionaryName), "dictionaryName");
        if (!string.IsNullOrEmpty(languageFrom))
            formContent.Add(new StringContent(languageFrom), "languageFrom");
        if (!string.IsNullOrEmpty(languageTo))
            formContent.Add(new StringContent(languageTo), "languageTo");

        var response = await _httpClient.PostAsync("api/dictionaries/import/anki", formContent);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            var msg = TryExtractMessage(body) ?? body;
            throw new HttpRequestException(msg);
        }

        var result = await response.Content.ReadFromJsonAsync<ImportResult>();
        return result ?? new ImportResult();
    }

    #endregion

    #region Create

    public async Task<CreatedDictionaryResult> CreateDictionaryAsync(string name, string description,
        string languageFrom, string languageTo, string? tags = null)
    {
        await ApplyAuthAsync();
        var request = new { Name = name, Description = description, LanguageFrom = languageFrom, LanguageTo = languageTo, Tags = tags };
        var response = await _httpClient.PostAsJsonAsync("api/dictionaries", request);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException(TryExtractMessage(body) ?? $"Ошибка создания словаря: {response.StatusCode}");
        }
        return await response.Content.ReadFromJsonAsync<CreatedDictionaryResult>() ?? new();
    }

    public async Task<CreatedWordResult> AddWordAsync(int dictionaryId, string originalWord, string translation, string? example = null)
    {
        await ApplyAuthAsync();
        var request = new { DictionaryId = dictionaryId, OriginalWord = originalWord, Translation = translation, Example = example ?? "" };
        var response = await _httpClient.PostAsJsonAsync("api/words", request);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException(TryExtractMessage(body) ?? $"Ошибка добавления слова: {response.StatusCode}");
        }
        return await response.Content.ReadFromJsonAsync<CreatedWordResult>() ?? new();
    }

    public async Task<CreatedRuleResult> CreateRuleAsync(string title, string markdownContent, string description,
        string category, int difficultyLevel, List<CreateExerciseInput>? exercises = null)
    {
        await ApplyAuthAsync();
        var exercisesList = exercises?.Select((e, idx) => new
            {
                e.ExerciseType,
                e.Question,
                e.Options,
                e.CorrectIndex,
                e.CorrectAnswer,
                e.AlternativeAnswersJson,
                e.IncorrectSentence,
                e.ShuffledWordsJson,
                e.Explanation,
                OrderIndex = idx,
                e.DifficultyTier
            }).ToArray();
        var request = new
        {
            Title = title,
            MarkdownContent = markdownContent,
            Description = description,
            Category = category,
            DifficultyLevel = difficultyLevel,
            CreatedAt = DateTime.UtcNow,
            Exercises = exercisesList ?? Array.Empty<object>()
        };
        var response = await _httpClient.PostAsJsonAsync("api/rules", request);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException(TryExtractMessage(body) ?? $"Ошибка создания правила: {response.StatusCode}");
        }
        return await response.Content.ReadFromJsonAsync<CreatedRuleResult>() ?? new();
    }

    public async Task UpdateRuleAsync(int id, string title, string markdownContent, string description,
        string category, int difficultyLevel, List<CreateExerciseInput>? exercises = null)
    {
        await ApplyAuthAsync();
        var exercisesList = exercises?.Select((e, idx) => new
        {
            e.ExerciseType,
            e.Question,
            OptionsJson = System.Text.Json.JsonSerializer.Serialize(e.Options ?? Array.Empty<string>()),
            e.CorrectIndex,
            e.CorrectAnswer,
            e.AlternativeAnswersJson,
            e.IncorrectSentence,
            e.ShuffledWordsJson,
            e.Explanation,
            OrderIndex = idx,
            e.DifficultyTier
        }).ToArray();
        var request = new
        {
            Id = id,
            Title = title,
            MarkdownContent = markdownContent,
            Description = description,
            Category = category,
            DifficultyLevel = difficultyLevel,
            Exercises = exercisesList ?? Array.Empty<object>()
        };
        var response = await _httpClient.PutAsJsonAsync($"api/rules/{id}", request);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException(TryExtractMessage(body) ?? $"Ошибка обновления правила: {response.StatusCode}");
        }
    }

    public async Task DeleteRuleAsync(int id)
    {
        await ApplyAuthAsync();
        var response = await _httpClient.DeleteAsync($"api/rules/{id}");
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException(TryExtractMessage(body) ?? $"Ошибка удаления правила: {response.StatusCode}");
        }
    }

    #endregion

    #region Export

    public async Task<ExportResult> ExportDictionaryAsJsonAsync(int dictionaryId)
    {
        await ApplyAuthAsync();
        var response = await _httpClient.GetAsync($"api/dictionaries/export/{dictionaryId}/json");
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsByteArrayAsync();
        var fileName = GetFileNameFromResponse(response) ?? $"dictionary_{dictionaryId}.json";

        return new ExportResult { Data = content, FileName = fileName, ContentType = "application/json" };
    }

    public async Task<ExportResult> ExportDictionaryAsCsvAsync(int dictionaryId)
    {
        await ApplyAuthAsync();
        var response = await _httpClient.GetAsync($"api/dictionaries/export/{dictionaryId}/csv");
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsByteArrayAsync();
        var fileName = GetFileNameFromResponse(response) ?? $"dictionary_{dictionaryId}.csv";

        return new ExportResult { Data = content, FileName = fileName, ContentType = "text/csv" };
    }

    public async Task<ExportResult> ExportAllDictionariesAsZipAsync()
    {
        await ApplyAuthAsync();
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

    #region Platform Stats

    public async Task<PlatformStatsResponse> GetPlatformStatsAsync()
    {
        var result = await _httpClient.GetFromJsonAsync<PlatformStatsResponse>("api/marketplace/stats");
        return result ?? new PlatformStatsResponse();
    }

    #endregion

    #region User Profiles

    public async Task<UserPublicProfile?> GetUserProfileAsync(int userId)
    {
        try
        {
            await ApplyAuthAsync();
            return await _httpClient.GetFromJsonAsync<UserPublicProfile>($"api/users/{userId}/profile");
        }
        catch (HttpRequestException)
        {
            return null;
        }
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
    public int AuthorId { get; set; }
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
    public int AuthorId { get; set; }
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
    public List<ExerciseItem> Exercises { get; set; } = new();
}

public class CommentItem
{
    public int Id { get; set; }
    public int AuthorId { get; set; }
    public string AuthorName { get; set; } = "";
    public int Rating { get; set; }
    public string Text { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public class MyDictionaryItem
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int WordCount { get; set; }
    public bool IsPublished { get; set; }
    public double Rating { get; set; }
    public int Downloads { get; set; }
    public string? Tags { get; set; }
    public bool IsSharedWithStudents { get; set; }
    public int SharedStudentCount { get; set; }
    public bool IsFromTeacher { get; set; }
    public string? TeacherName { get; set; }
}

public class MyDictionaryFullDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string LanguageFrom { get; set; } = "";
    public string LanguageTo { get; set; } = "";
    public List<MyDictionaryWordDto> Words { get; set; } = new();
}

public class MyDictionaryWordDto
{
    public int Id { get; set; }
    public string OriginalWord { get; set; } = "";
    public string Translation { get; set; } = "";
    public string? Transcription { get; set; }
    public string? Example { get; set; }
}

public class MyRuleItem
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Category { get; set; } = "";
    public bool IsPublished { get; set; }
    public double Rating { get; set; }
    public int Downloads { get; set; }
    public bool IsSharedWithStudents { get; set; }
    public int SharedStudentCount { get; set; }
    public bool IsFromTeacher { get; set; }
    public string? TeacherName { get; set; }
}

public class DownloadedItem
{
    public int Id { get; set; }
    public string Type { get; set; } = "";
    public string Title { get; set; } = "";
    public string AuthorName { get; set; } = "";
    public DateTime DownloadedAt { get; set; }
}

public class MyRuleDetailItem
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Category { get; set; } = "";
    public int DifficultyLevel { get; set; }
    public bool IsPublished { get; set; }
    public double Rating { get; set; }
    public int Downloads { get; set; }
    public string HtmlContent { get; set; } = "";
    public string MarkdownContent { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public List<ExerciseItem> Exercises { get; set; } = new();
}

public class ExerciseItem
{
    public int Id { get; set; }
    public string ExerciseType { get; set; } = "mcq";
    public string Question { get; set; } = "";
    public string[] Options { get; set; } = Array.Empty<string>();
    public int CorrectIndex { get; set; }
    public string? CorrectAnswer { get; set; }
    public string? AlternativeAnswersJson { get; set; }
    public string? IncorrectSentence { get; set; }
    public string? ShuffledWordsJson { get; set; }
    public string Explanation { get; set; } = "";
    public int OrderIndex { get; set; }
    public int DifficultyTier { get; set; } = 1;
}

public class ExportResult
{
    public byte[] Data { get; set; } = Array.Empty<byte>();
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
}

public class ImportResult
{
    public string Message { get; set; } = "";
    public int DictionaryId { get; set; }
    public string Name { get; set; } = "";
    public int WordCount { get; set; }
    public ImportDetectedMapping? DetectedMapping { get; set; }
}

public class ImportDetectedMapping
{
    public string? Original { get; set; }
    public string? Translation { get; set; }
    public string? Transcription { get; set; }
    public string? Example { get; set; }
}

public class PlatformStatsResponse
{
    public int DictionaryCount { get; set; }
    public int RuleCount { get; set; }
    public int UserCount { get; set; }
}

public class UserPublicProfile
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
    public List<PublishedContentItem> PublishedDictionaries { get; set; } = new();
    public List<PublishedContentItem> PublishedRules { get; set; } = new();
}

public class PublishedContentItem
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public double Rating { get; set; }
    public int DownloadCount { get; set; }
}

public class CreatedDictionaryResult
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

public class CreatedWordResult
{
    public int Id { get; set; }
    public string OriginalWord { get; set; } = "";
    public string Translation { get; set; } = "";
}

public class CreatedRuleResult
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
}

public class CreateExerciseInput
{
    public string ExerciseType { get; set; } = "mcq";
    public string Question { get; set; } = "";
    public string[] Options { get; set; } = Array.Empty<string>();
    public int CorrectIndex { get; set; }
    public string? CorrectAnswer { get; set; }
    public string? AlternativeAnswersJson { get; set; }
    public string? IncorrectSentence { get; set; }
    public string? ShuffledWordsJson { get; set; }
    public string Explanation { get; set; } = "";
    public int DifficultyTier { get; set; } = 1;
}

#endregion
