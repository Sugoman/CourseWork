namespace LearningTrainerShared.Models.Features.Ai;

/// <summary>
/// Единый контракт для AI-перевода и генерации примеров.
/// Реализуется HTTP-клиентом, который обращается к Ingat.AI-сервису.
/// </summary>
public interface IAiTranslationService
{
    /// <summary>
    /// Генеративный перевод слова/фразы (LLM, не lookup).
    /// </summary>
    /// <param name="partOfSpeech">Часть речи (noun, verb, adjective...) для устранения неоднозначности. null = авто.</param>
    Task<AiTranslateResult?> TranslateAsync(
        string word, string sourceLanguage, string targetLanguage,
        string? partOfSpeech = null,
        CancellationToken ct = default);

    /// <summary>
    /// Генерация примеров предложений с использованием слова.
    /// </summary>
    /// <param name="partOfSpeech">Часть речи для контекста.</param>
    /// <param name="languageLevel">Уровень CEFR (A1–C2) для адаптации сложности примера.</param>
    Task<List<AiExampleResult>> GetExamplesAsync(
        string word, string language, string targetLanguage,
        string? partOfSpeech = null, string? languageLevel = null,
        int count = 1, CancellationToken ct = default);

    /// <summary>
    /// Генерация целого словаря по теме с помощью LLM.
    /// </summary>
    Task<List<AiGeneratedWordEntry>> GenerateDictionaryAsync(
        string topic, string sourceLanguage, string targetLanguage,
        string languageLevel, int wordCount,
        CancellationToken ct = default);
}
