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

    public async Task<List<AiGeneratedWordEntry>> GenerateDictionaryAsync(
        string topic, string sourceLanguage, string targetLanguage,
        string languageLevel, int wordCount,
        CancellationToken ct = default)
    {
        var payload = new
        {
            topic,
            sourceLanguage,
            targetLanguage,
            languageLevel,
            wordCount
        };

        var response = await _httpClient.PostAsJsonAsync("/api/ai/generate-dictionary", payload, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            Debug.WriteLine($"AI generate-dictionary failed: {response.StatusCode} — {body}");
            return new List<AiGeneratedWordEntry>();
        }

        var dto = await response.Content.ReadFromJsonAsync<AiGeneratedDictDto>(JsonOptions, ct);
        if (dto?.Words == null)
            return new List<AiGeneratedWordEntry>();

        return dto.Words
            .Where(w => !string.IsNullOrWhiteSpace(w.Original) && !string.IsNullOrWhiteSpace(w.Translation))
            .Select(w => new AiGeneratedWordEntry(w.Original, w.Translation, w.PartOfSpeech ?? "", w.Example ?? ""))
            .ToList();
    }

    public async Task<List<AiBatchTranslateItem>> TranslateBatchAsync(
        List<string> words, string sourceLanguage, string targetLanguage,
        CancellationToken ct = default)
    {
        var payload = new
        {
            words,
            sourceLanguage,
            targetLanguage
        };

        var response = await _httpClient.PostAsJsonAsync("/api/ai/translate-batch", payload, ct);

        if (!response.IsSuccessStatusCode)
        {
            Debug.WriteLine($"AI translate-batch failed: {response.StatusCode}");
            return new List<AiBatchTranslateItem>();
        }

        var dto = await response.Content.ReadFromJsonAsync<AiBatchTranslateDto>(JsonOptions, ct);
        if (dto?.Translations == null)
            return new List<AiBatchTranslateItem>();

        return dto.Translations
            .Where(t => !string.IsNullOrWhiteSpace(t.Word) && !string.IsNullOrWhiteSpace(t.Translation))
            .Select(t => new AiBatchTranslateItem(t.Word, t.Translation, t.Alternatives ?? new List<string>()))
            .ToList();
    }

    // === Phase 3: New AI Features ===

    public async Task<List<AiExerciseResult>> GenerateExercisesAsync(
        string ruleTitle, string ruleContent, string language, string targetLanguage,
        int count = 5, string? languageLevel = null, CancellationToken ct = default)
    {
        try
        {
            var payload = new { ruleTitle, ruleContent, language, targetLanguage, count, languageLevel };
            var response = await _httpClient.PostAsJsonAsync("/api/ai/generate-exercises", payload, ct);
            if (!response.IsSuccessStatusCode) return new List<AiExerciseResult>();

            var dto = await response.Content.ReadFromJsonAsync<AiExercisesDto>(JsonOptions, ct);
            return dto?.Exercises?
                .Where(e => !string.IsNullOrWhiteSpace(e.Question) && e.Options?.Count >= 2)
                .Select(e => new AiExerciseResult(e.Question, e.Options ?? new(), e.CorrectIndex, e.Explanation ?? ""))
                .ToList() ?? new();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"AI generate-exercises failed: {ex.Message}");
            return new List<AiExerciseResult>();
        }
    }

    public async Task<AiMistakeExplanation?> ExplainMistakeAsync(
        string word, string userAnswer, string correctAnswer,
        string language, string targetLanguage, string? context = null, CancellationToken ct = default)
    {
        try
        {
            var payload = new { word, userAnswer, correctAnswer, context, language, targetLanguage };
            var response = await _httpClient.PostAsJsonAsync("/api/ai/explain-mistake", payload, ct);
            if (!response.IsSuccessStatusCode) return null;

            var dto = await response.Content.ReadFromJsonAsync<AiExplainDto>(JsonOptions, ct);
            if (dto == null || string.IsNullOrWhiteSpace(dto.Explanation)) return null;
            return new AiMistakeExplanation(dto.Explanation, dto.Tip);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"AI explain-mistake failed: {ex.Message}");
            return null;
        }
    }

    public async Task<AiMnemonicResult?> GenerateMnemonicAsync(
        string word, string translation, string language, string targetLanguage, CancellationToken ct = default)
    {
        try
        {
            var payload = new { word, translation, language, targetLanguage };
            var response = await _httpClient.PostAsJsonAsync("/api/ai/mnemonic", payload, ct);
            if (!response.IsSuccessStatusCode) return null;

            var dto = await response.Content.ReadFromJsonAsync<AiMnemonicDto>(JsonOptions, ct);
            if (dto == null || string.IsNullOrWhiteSpace(dto.Mnemonic)) return null;
            return new AiMnemonicResult(dto.Mnemonic, dto.Etymology, dto.Association);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"AI mnemonic failed: {ex.Message}");
            return null;
        }
    }

    public async Task<AiDetectedLanguage?> DetectLanguageAsync(string text, CancellationToken ct = default)
    {
        try
        {
            var payload = new { text };
            var response = await _httpClient.PostAsJsonAsync("/api/ai/detect-language", payload, ct);
            if (!response.IsSuccessStatusCode) return null;

            var dto = await response.Content.ReadFromJsonAsync<AiDetectLangDto>(JsonOptions, ct);
            if (dto == null || string.IsNullOrWhiteSpace(dto.Language)) return null;
            return new AiDetectedLanguage(dto.Language, dto.Confidence);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"AI detect-language failed: {ex.Message}");
            return null;
        }
    }

    public async Task<List<AiExtractedWord>> ExtractWordsFromTextAsync(
        string text, string language, string targetLanguage,
        int maxWords = 20, string? languageLevel = null, CancellationToken ct = default)
    {
        try
        {
            var payload = new { text, language, targetLanguage, maxWords, languageLevel };
            var response = await _httpClient.PostAsJsonAsync("/api/ai/extract-words", payload, ct);
            if (!response.IsSuccessStatusCode) return new List<AiExtractedWord>();

            var dto = await response.Content.ReadFromJsonAsync<AiExtractWordsDto>(JsonOptions, ct);
            return dto?.Words?
                .Where(w => !string.IsNullOrWhiteSpace(w.Original) && !string.IsNullOrWhiteSpace(w.Translation))
                .Select(w => new AiExtractedWord(w.Original, w.Translation, w.PartOfSpeech, w.Context))
                .ToList() ?? new();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"AI extract-words failed: {ex.Message}");
            return new List<AiExtractedWord>();
        }
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

    private sealed class AiGeneratedDictDto
    {
        public List<AiGeneratedWordDto>? Words { get; set; }
    }

    private sealed class AiGeneratedWordDto
    {
        public string Original { get; set; } = string.Empty;
        public string Translation { get; set; } = string.Empty;
        public string? PartOfSpeech { get; set; }
        public string? Example { get; set; }
    }

    private sealed class AiBatchTranslateDto
    {
        public List<AiBatchTranslateItemDto>? Translations { get; set; }
    }

    private sealed class AiBatchTranslateItemDto
    {
        public string Word { get; set; } = string.Empty;
        public string Translation { get; set; } = string.Empty;
        public List<string>? Alternatives { get; set; }
    }

    // Phase 3 DTOs

    private sealed class AiExercisesDto
    {
        public List<AiExerciseItemDto>? Exercises { get; set; }
    }

    private sealed class AiExerciseItemDto
    {
        public string Question { get; set; } = string.Empty;
        public List<string>? Options { get; set; }
        public int CorrectIndex { get; set; }
        public string? Explanation { get; set; }
    }

    private sealed class AiExplainDto
    {
        public string Explanation { get; set; } = string.Empty;
        public string? Tip { get; set; }
    }

    private sealed class AiMnemonicDto
    {
        public string Mnemonic { get; set; } = string.Empty;
        public string? Etymology { get; set; }
        public string? Association { get; set; }
    }

    private sealed class AiDetectLangDto
    {
        public string Language { get; set; } = string.Empty;
        public double Confidence { get; set; }
    }

    private sealed class AiExtractWordsDto
    {
        public List<AiExtractedWordDto>? Words { get; set; }
    }

    private sealed class AiExtractedWordDto
    {
        public string Original { get; set; } = string.Empty;
        public string Translation { get; set; } = string.Empty;
        public string? PartOfSpeech { get; set; }
        public string? Context { get; set; }
    }

    #endregion
}
