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

    /// <summary>
    /// Batch-перевод нескольких слов одним запросом к LLM.
    /// Значительно быстрее последовательного перевода при N > 3.
    /// </summary>
    Task<List<AiBatchTranslateItem>> TranslateBatchAsync(
        List<string> words, string sourceLanguage, string targetLanguage,
        CancellationToken ct = default);

    // === Phase 3: New AI Features ===

    /// <summary>
    /// Генерация грамматических упражнений (fill-in-the-blank) по правилу.
    /// </summary>
    Task<List<AiExerciseResult>> GenerateExercisesAsync(
        string ruleTitle, string ruleContent, string language, string targetLanguage,
        int count = 5, string? languageLevel = null,
        CancellationToken ct = default);

    /// <summary>
    /// Объяснение ошибки пользователя в тренировке (почему правильный ответ — именно этот).
    /// </summary>
    Task<AiMistakeExplanation?> ExplainMistakeAsync(
        string word, string userAnswer, string correctAnswer,
        string language, string targetLanguage,
        string? context = null,
        CancellationToken ct = default);

    /// <summary>
    /// Генерация мнемоники для запоминания слова (ассоциация, этимология, визуальный образ).
    /// </summary>
    Task<AiMnemonicResult?> GenerateMnemonicAsync(
        string word, string translation, string language, string targetLanguage,
        CancellationToken ct = default);

    /// <summary>
    /// Автоопределение языка текста.
    /// </summary>
    Task<AiDetectedLanguage?> DetectLanguageAsync(
        string text, CancellationToken ct = default);

    /// <summary>
    /// Извлечение ключевых слов из текста с переводом. Пользователь вставляет текст → ИИ создаёт словарь.
    /// </summary>
    Task<List<AiExtractedWord>> ExtractWordsFromTextAsync(
        string text, string language, string targetLanguage,
        int maxWords = 20, string? languageLevel = null,
        CancellationToken ct = default);
}
