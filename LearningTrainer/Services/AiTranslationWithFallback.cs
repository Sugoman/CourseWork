using LearningTrainerShared.Models.Features.Ai;
using System.Diagnostics;
using System.Net.Http;

namespace LearningTrainer.Services;

/// <summary>
/// Обёртка: сначала пробует AI-сервис, при неудаче — откатывается на fallback.
/// Перевод: AI → MyMemory.
/// Примеры: AI → dictionaryapi.dev.
/// </summary>
public sealed class AiTranslationWithFallback : IAiTranslationService
{
    private readonly AiTranslationHttpService _ai;
    private readonly TranslationService _translationFallback;
    private readonly ExternalDictionaryService _exampleFallback;

    public AiTranslationWithFallback(
        AiTranslationHttpService ai,
        TranslationService translationFallback,
        ExternalDictionaryService exampleFallback)
    {
        _ai = ai;
        _translationFallback = translationFallback;
        _exampleFallback = exampleFallback;
    }

    public async Task<AiTranslateResult?> TranslateAsync(
        string word, string sourceLanguage, string targetLanguage,
        string? partOfSpeech = null,
        CancellationToken ct = default)
    {
        try
        {
            var result = await _ai.TranslateAsync(word, sourceLanguage, targetLanguage, partOfSpeech, ct);
            if (result != null)
                return result;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"AI translate unavailable, falling back to MyMemory: {ex.Message}");
        }

        // Fallback на MyMemory (без partOfSpeech — MyMemory не поддерживает)
        var fallbackResult = await _translationFallback.TranslateAsync(word, sourceLanguage, targetLanguage);
        return fallbackResult != null
            ? new AiTranslateResult(fallbackResult, new List<string>())
            : null;
    }

    public async Task<List<AiExampleResult>> GetExamplesAsync(
        string word, string language, string targetLanguage,
        string? partOfSpeech = null, string? languageLevel = null,
        int count = 1, CancellationToken ct = default)
    {
        // Сначала пробуем AI
        try
        {
            var result = await _ai.GetExamplesAsync(word, language, targetLanguage, partOfSpeech, languageLevel, count, ct);
            if (result.Count > 0)
                return result;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"AI example unavailable, falling back to dictionaryapi.dev: {ex.Message}");
        }

        // Fallback на dictionaryapi.dev (без partOfSpeech/level — API не поддерживает)
        try
        {
            var details = await _exampleFallback.GetWordDetailsAsync(word);
            if (details?.Example != null)
            {
                return new List<AiExampleResult>
                {
                    new(details.Example, "")
                };
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"dictionaryapi.dev fallback also failed: {ex.Message}");
        }

        return new List<AiExampleResult>();
    }

    public async Task<List<AiGeneratedWordEntry>> GenerateDictionaryAsync(
        string topic, string sourceLanguage, string targetLanguage,
        string languageLevel, int wordCount,
        CancellationToken ct = default)
    {
        // Генерация словаря — только через AI, fallback невозможен
        return await _ai.GenerateDictionaryAsync(topic, sourceLanguage, targetLanguage, languageLevel, wordCount, ct);
    }

    public async Task<List<AiBatchTranslateItem>> TranslateBatchAsync(
        List<string> words, string sourceLanguage, string targetLanguage,
        CancellationToken ct = default)
    {
        try
        {
            var result = await _ai.TranslateBatchAsync(words, sourceLanguage, targetLanguage, ct);
            if (result.Count > 0)
                return result;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"AI batch translate unavailable, falling back to sequential: {ex.Message}");
        }

        // Fallback: последовательный перевод каждого слова
        var items = new List<AiBatchTranslateItem>();
        foreach (var word in words)
        {
            try
            {
                var single = await TranslateAsync(word, sourceLanguage, targetLanguage, ct: ct);
                if (single != null)
                    items.Add(new AiBatchTranslateItem(word, single.Translation, single.Alternatives));
            }
            catch { }
        }
        return items;
    }

    // === Phase 3: New AI Features (no fallback — AI only) ===

    public Task<List<AiExerciseResult>> GenerateExercisesAsync(
        string ruleTitle, string ruleContent, string language, string targetLanguage,
        int count = 5, string? languageLevel = null, CancellationToken ct = default)
        => _ai.GenerateExercisesAsync(ruleTitle, ruleContent, language, targetLanguage, count, languageLevel, ct);

    public Task<AiMistakeExplanation?> ExplainMistakeAsync(
        string word, string userAnswer, string correctAnswer,
        string language, string targetLanguage, string? context = null, CancellationToken ct = default)
        => _ai.ExplainMistakeAsync(word, userAnswer, correctAnswer, language, targetLanguage, context, ct);

    public Task<AiMnemonicResult?> GenerateMnemonicAsync(
        string word, string translation, string language, string targetLanguage, CancellationToken ct = default)
        => _ai.GenerateMnemonicAsync(word, translation, language, targetLanguage, ct);

    public Task<AiDetectedLanguage?> DetectLanguageAsync(string text, CancellationToken ct = default)
        => _ai.DetectLanguageAsync(text, ct);

    public Task<List<AiExtractedWord>> ExtractWordsFromTextAsync(
        string text, string language, string targetLanguage,
        int maxWords = 20, string? languageLevel = null, CancellationToken ct = default)
        => _ai.ExtractWordsFromTextAsync(text, language, targetLanguage, maxWords, languageLevel, ct);
}
