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
}
