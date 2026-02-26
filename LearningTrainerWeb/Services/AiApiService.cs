using LearningTrainerShared.Models.Features.Ai;
using System.Net.Http.Json;
using System.Text.Json;

namespace LearningTrainerWeb.Services;

/// <summary>
/// Сервис для работы с ИИ-функциями (перевод, примеры, генерация словарей).
/// HTTP-клиент к Ingat.AI.
/// </summary>
public interface IAiApiService
{
    Task<AiTranslateResult?> TranslateAsync(string word, string sourceLanguage, string targetLanguage,
        string? partOfSpeech = null, CancellationToken ct = default);

    Task<List<AiExampleResult>> GetExamplesAsync(string word, string language, string targetLanguage,
        int count = 1, string? partOfSpeech = null, string? languageLevel = null, CancellationToken ct = default);

    Task<List<AiGeneratedWordEntry>> GenerateDictionaryAsync(string topic, string sourceLanguage,
        string targetLanguage, string languageLevel, int wordCount, CancellationToken ct = default);

    Task<bool> CheckHealthAsync(CancellationToken ct = default);

    // Phase 3
    Task<List<AiExerciseResult>> GenerateExercisesAsync(string ruleTitle, string ruleContent,
        string language, string targetLanguage, int count = 5, string? languageLevel = null,
        CancellationToken ct = default);

    Task<AiMistakeExplanation?> ExplainMistakeAsync(string word, string userAnswer, string correctAnswer,
        string language, string targetLanguage, string? context = null, CancellationToken ct = default);

    Task<AiMnemonicResult?> GenerateMnemonicAsync(string word, string translation,
        string language, string targetLanguage, CancellationToken ct = default);

    Task<AiDetectedLanguage?> DetectLanguageAsync(string text, CancellationToken ct = default);

    Task<List<AiExtractedWord>> ExtractWordsFromTextAsync(string text, string language, string targetLanguage,
        int maxWords = 20, string? languageLevel = null, CancellationToken ct = default);
}

public class AiApiService : IAiApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AiApiService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public AiApiService(HttpClient httpClient, ILogger<AiApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<AiTranslateResult?> TranslateAsync(string word, string sourceLanguage, string targetLanguage,
        string? partOfSpeech = null, CancellationToken ct = default)
    {
        try
        {
            var payload = new { word, sourceLanguage, targetLanguage, partOfSpeech };
            var response = await _httpClient.PostAsJsonAsync("/api/ai/translate", payload, ct);

            if (!response.IsSuccessStatusCode)
                return null;

            var dto = await response.Content.ReadFromJsonAsync<TranslateDto>(JsonOptions, ct);
            if (dto == null || string.IsNullOrWhiteSpace(dto.Translation))
                return null;

            return new AiTranslateResult(dto.Translation, dto.Alternatives ?? new List<string>());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI translate failed for '{Word}'", word);
            return null;
        }
    }

    public async Task<List<AiExampleResult>> GetExamplesAsync(string word, string language, string targetLanguage,
        int count = 1, string? partOfSpeech = null, string? languageLevel = null, CancellationToken ct = default)
    {
        try
        {
            var payload = new { word, language, targetLanguage, count, partOfSpeech, languageLevel };
            var response = await _httpClient.PostAsJsonAsync("/api/ai/example", payload, ct);

            if (!response.IsSuccessStatusCode)
                return new List<AiExampleResult>();

            var dto = await response.Content.ReadFromJsonAsync<ExampleDto>(JsonOptions, ct);
            if (dto?.Examples == null)
                return new List<AiExampleResult>();

            return dto.Examples
                .Where(e => !string.IsNullOrWhiteSpace(e.Sentence))
                .Select(e => new AiExampleResult(e.Sentence, e.Translation ?? ""))
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI examples failed for '{Word}'", word);
            return new List<AiExampleResult>();
        }
    }

    public async Task<List<AiGeneratedWordEntry>> GenerateDictionaryAsync(string topic, string sourceLanguage,
        string targetLanguage, string languageLevel, int wordCount, CancellationToken ct = default)
    {
        try
        {
            var payload = new { topic, sourceLanguage, targetLanguage, languageLevel, wordCount };
            var response = await _httpClient.PostAsJsonAsync("/api/ai/generate-dictionary", payload, ct);

            if (!response.IsSuccessStatusCode)
                return new List<AiGeneratedWordEntry>();

            var dto = await response.Content.ReadFromJsonAsync<GenerateDictDto>(JsonOptions, ct);
            if (dto?.Words == null)
                return new List<AiGeneratedWordEntry>();

            return dto.Words
                .Where(w => !string.IsNullOrWhiteSpace(w.Original) && !string.IsNullOrWhiteSpace(w.Translation))
                .Select(w => new AiGeneratedWordEntry(w.Original, w.Translation, w.PartOfSpeech ?? "", w.Example ?? ""))
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI generate-dictionary failed for '{Topic}'", topic);
            return new List<AiGeneratedWordEntry>();
        }
    }

    public async Task<bool> CheckHealthAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/health", ct);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    // === Phase 3 implementations ===

    public async Task<List<AiExerciseResult>> GenerateExercisesAsync(string ruleTitle, string ruleContent,
        string language, string targetLanguage, int count = 5, string? languageLevel = null,
        CancellationToken ct = default)
    {
        try
        {
            var payload = new { ruleTitle, ruleContent, language, targetLanguage, count, languageLevel };
            var response = await _httpClient.PostAsJsonAsync("/api/ai/generate-exercises", payload, ct);

            if (!response.IsSuccessStatusCode)
                return new List<AiExerciseResult>();

            var dto = await response.Content.ReadFromJsonAsync<ExercisesDto>(JsonOptions, ct);
            if (dto?.Exercises == null)
                return new List<AiExerciseResult>();

            return dto.Exercises
                .Where(e => !string.IsNullOrWhiteSpace(e.Question) && e.Options?.Count >= 2)
                .Select(e => new AiExerciseResult(e.Question, e.Options ?? new(), e.CorrectIndex, e.Explanation ?? ""))
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI generate-exercises failed for '{Rule}'", ruleTitle);
            return new List<AiExerciseResult>();
        }
    }

    public async Task<AiMistakeExplanation?> ExplainMistakeAsync(string word, string userAnswer,
        string correctAnswer, string language, string targetLanguage, string? context = null,
        CancellationToken ct = default)
    {
        try
        {
            var payload = new { word, userAnswer, correctAnswer, context, language, targetLanguage };
            var response = await _httpClient.PostAsJsonAsync("/api/ai/explain-mistake", payload, ct);

            if (!response.IsSuccessStatusCode)
                return null;

            var dto = await response.Content.ReadFromJsonAsync<ExplainDto>(JsonOptions, ct);
            if (dto == null || string.IsNullOrWhiteSpace(dto.Explanation))
                return null;

            return new AiMistakeExplanation(dto.Explanation, dto.Tip);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI explain-mistake failed for '{Word}'", word);
            return null;
        }
    }

    public async Task<AiMnemonicResult?> GenerateMnemonicAsync(string word, string translation,
        string language, string targetLanguage, CancellationToken ct = default)
    {
        try
        {
            var payload = new { word, translation, language, targetLanguage };
            var response = await _httpClient.PostAsJsonAsync("/api/ai/mnemonic", payload, ct);

            if (!response.IsSuccessStatusCode)
                return null;

            var dto = await response.Content.ReadFromJsonAsync<MnemonicDto>(JsonOptions, ct);
            if (dto == null || string.IsNullOrWhiteSpace(dto.Mnemonic))
                return null;

            return new AiMnemonicResult(dto.Mnemonic, dto.Etymology, dto.Association);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI mnemonic failed for '{Word}'", word);
            return null;
        }
    }

    public async Task<AiDetectedLanguage?> DetectLanguageAsync(string text, CancellationToken ct = default)
    {
        try
        {
            var payload = new { text };
            var response = await _httpClient.PostAsJsonAsync("/api/ai/detect-language", payload, ct);

            if (!response.IsSuccessStatusCode)
                return null;

            var dto = await response.Content.ReadFromJsonAsync<DetectLangDto>(JsonOptions, ct);
            if (dto == null || string.IsNullOrWhiteSpace(dto.Language))
                return null;

            return new AiDetectedLanguage(dto.Language, dto.Confidence);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI detect-language failed");
            return null;
        }
    }

    public async Task<List<AiExtractedWord>> ExtractWordsFromTextAsync(string text, string language,
        string targetLanguage, int maxWords = 20, string? languageLevel = null, CancellationToken ct = default)
    {
        try
        {
            var payload = new { text, language, targetLanguage, maxWords, languageLevel };
            var response = await _httpClient.PostAsJsonAsync("/api/ai/extract-words", payload, ct);

            if (!response.IsSuccessStatusCode)
                return new List<AiExtractedWord>();

            var dto = await response.Content.ReadFromJsonAsync<ExtractWordsDto>(JsonOptions, ct);
            if (dto?.Words == null)
                return new List<AiExtractedWord>();

            return dto.Words
                .Where(w => !string.IsNullOrWhiteSpace(w.Original) && !string.IsNullOrWhiteSpace(w.Translation))
                .Select(w => new AiExtractedWord(w.Original, w.Translation, w.PartOfSpeech, w.Context))
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI extract-words failed");
            return new List<AiExtractedWord>();
        }
    }

    #region Internal DTOs

    private sealed class TranslateDto
    {
        public string Translation { get; set; } = string.Empty;
        public List<string>? Alternatives { get; set; }
    }

    private sealed class ExampleDto
    {
        public List<ExampleItemDto>? Examples { get; set; }
    }

    private sealed class ExampleItemDto
    {
        public string Sentence { get; set; } = string.Empty;
        public string Translation { get; set; } = string.Empty;
    }

    private sealed class GenerateDictDto
    {
        public List<GeneratedWordDto>? Words { get; set; }
    }

    private sealed class GeneratedWordDto
    {
        public string Original { get; set; } = string.Empty;
        public string Translation { get; set; } = string.Empty;
        public string? PartOfSpeech { get; set; }
        public string? Example { get; set; }
    }

    // Phase 3 DTOs

    private sealed class ExercisesDto
    {
        public List<ExerciseItemDto>? Exercises { get; set; }
    }

    private sealed class ExerciseItemDto
    {
        public string Question { get; set; } = string.Empty;
        public List<string>? Options { get; set; }
        public int CorrectIndex { get; set; }
        public string? Explanation { get; set; }
    }

    private sealed class ExplainDto
    {
        public string Explanation { get; set; } = string.Empty;
        public string? Tip { get; set; }
    }

    private sealed class MnemonicDto
    {
        public string Mnemonic { get; set; } = string.Empty;
        public string? Etymology { get; set; }
        public string? Association { get; set; }
    }

    private sealed class DetectLangDto
    {
        public string Language { get; set; } = string.Empty;
        public double Confidence { get; set; }
    }

    private sealed class ExtractWordsDto
    {
        public List<ExtractedWordDto>? Words { get; set; }
    }

    private sealed class ExtractedWordDto
    {
        public string Original { get; set; } = string.Empty;
        public string Translation { get; set; } = string.Empty;
        public string? PartOfSpeech { get; set; }
        public string? Context { get; set; }
    }

    #endregion
}
