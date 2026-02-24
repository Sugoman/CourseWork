using LearningTrainerShared.Models.Features.Ai;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace LearningTrainer.Services;

/// <summary>
/// Реализация IAiTranslationService — HTTP-клиент к сервису Ingat.AI.
/// </summary>
public sealed class AiTranslationHttpService : IAiTranslationService
{
    private readonly HttpClient _httpClient;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public AiTranslationHttpService(string baseUrl = "http://localhost:5200")
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(baseUrl),
            Timeout = TimeSpan.FromSeconds(90)
        };
    }

    public async Task<AiTranslateResult?> TranslateAsync(
        string word, string sourceLanguage, string targetLanguage,
        string? partOfSpeech = null,
        CancellationToken ct = default)
    {
        var payload = new
        {
            word,
            sourceLanguage,
            targetLanguage,
            partOfSpeech
        };

        var response = await _httpClient.PostAsJsonAsync("/api/ai/translate", payload, ct);

        if (!response.IsSuccessStatusCode)
        {
            Debug.WriteLine($"AI translate failed: {response.StatusCode}");
            return null;
        }

        var dto = await response.Content.ReadFromJsonAsync<AiTranslateDto>(JsonOptions, ct);
        if (dto == null || string.IsNullOrWhiteSpace(dto.Translation))
            return null;

        return new AiTranslateResult(dto.Translation, dto.Alternatives ?? new List<string>());
    }

    public async Task<List<AiExampleResult>> GetExamplesAsync(
        string word, string language, string targetLanguage,
        string? partOfSpeech = null, string? languageLevel = null,
        int count = 1, CancellationToken ct = default)
    {
        var payload = new
        {
            word,
            language,
            targetLanguage,
            count,
            partOfSpeech,
            languageLevel
        };

        var response = await _httpClient.PostAsJsonAsync("/api/ai/example", payload, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            Debug.WriteLine($"AI example failed: {response.StatusCode} — {body}");
            return new List<AiExampleResult>();
        }

        var dto = await response.Content.ReadFromJsonAsync<AiExampleDto>(JsonOptions, ct);
        if (dto?.Examples == null)
            return new List<AiExampleResult>();

        return dto.Examples
            .Where(e => !string.IsNullOrWhiteSpace(e.Sentence))
            .Select(e => new AiExampleResult(e.Sentence, e.Translation ?? ""))
            .ToList();
    }

    #region Internal DTOs (для десериализации ответа AI-сервиса)

    private sealed class AiTranslateDto
    {
        public string Translation { get; set; } = string.Empty;
        public List<string>? Alternatives { get; set; }
    }

    private sealed class AiExampleDto
    {
        public List<AiExampleItemDto>? Examples { get; set; }
    }

    private sealed class AiExampleItemDto
    {
        public string Sentence { get; set; } = string.Empty;
        public string Translation { get; set; } = string.Empty;
    }

    #endregion
}
