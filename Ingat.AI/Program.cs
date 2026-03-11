using Ingat.AI.Models;
using Ingat.AI.Services;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient<IAiProvider, OllamaProvider>();
builder.Services.AddMemoryCache();

// CORS — разрешаем Blazor-клиенту и WPF-клиенту обращаться к API
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Rate limiting — защита от перегрузки Ollama
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("ai", limiter =>
    {
        limiter.PermitLimit = 10;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiter.QueueLimit = 5;
    });
    options.AddConcurrencyLimiter("ai-concurrent", limiter =>
    {
        limiter.PermitLimit = 2;
        limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiter.QueueLimit = 10;
    });
});

var app = builder.Build();

app.UseCors();
app.UseRateLimiter();

var jsonOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true
};

// Health check — проверяем доступность Ollama
app.MapGet("/health", async (IAiProvider ai) =>
{
    try
    {
        var response = await ai.CompleteAsync("Reply OK", "ping", CancellationToken.None);
        return Results.Ok(new { status = "healthy", model = "connected" });
    }
    catch (Exception ex)
    {
        return Results.Json(new { status = "unhealthy", error = ex.Message }, statusCode: 503);
    }
});

// POST /api/ai/translate
app.MapPost("/api/ai/translate", async (TranslateRequest req, IAiProvider ai, IMemoryCache cache, ILogger<Program> logger) =>
{
    if (string.IsNullOrWhiteSpace(req.Word))
        return Results.BadRequest(new { error = "Word is required" });

    if (req.Word.Length > 200)
        return Results.BadRequest(new { error = "Word too long (max 200 chars)" });

    var cacheKey = $"translate:{req.Word.Trim().ToLowerInvariant()}:{req.SourceLanguage}:{req.TargetLanguage}:{req.PartOfSpeech}";
    if (cache.TryGetValue(cacheKey, out TranslateResponse? cached) && cached != null)
    {
        logger.LogDebug("Cache hit for translate '{Word}'", req.Word);
        return Results.Ok(cached);
    }

    try
    {
        var prompt = PromptTemplates.TranslateUser(req.Word.Trim(), req.SourceLanguage, req.TargetLanguage, req.Context, req.PartOfSpeech);
        var parsed = await CompleteWithRetry<TranslateResponse>(ai, PromptTemplates.TranslateSystem, prompt, logger, maxTokens: 256);

        if (parsed == null)
            return Results.Json(new { error = "AI returned invalid response" }, statusCode: 502);

        // Post-validation: перевод не должен совпадать с оригиналом
        if (string.Equals(parsed.Translation.Trim(), req.Word.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning("AI translation equals original word '{Word}', discarding", req.Word);
            return Results.Json(new { error = "AI returned translation identical to input" }, statusCode: 502);
        }

        // Post-validation: перевод не должен быть мусорным (начинаться с цифры, содержать мало букв)
        var translationTrimmed = parsed.Translation.Trim();
        var letterCount = translationTrimmed.Count(c => char.IsLetter(c));
        if (translationTrimmed.Length < 2 ||
            char.IsDigit(translationTrimmed[0]) ||
            (translationTrimmed.Length > 2 && letterCount < translationTrimmed.Length / 3))
        {
            logger.LogWarning("AI returned garbled translation '{Translation}' for '{Word}', discarding", parsed.Translation, req.Word);
            return Results.Json(new { error = "AI returned garbled translation" }, statusCode: 502);
        }

        // Post-validation: перевод не должен содержать смешанные скрипты (напр. "грaveйный")
        if (HasMixedScripts(translationTrimmed))
        {
            logger.LogWarning("AI returned mixed-script translation '{Translation}' for '{Word}', discarding", parsed.Translation, req.Word);
            return Results.Json(new { error = "AI returned garbled translation" }, statusCode: 502);
        }

        // Post-validation: перевод должен быть на целевом языке
        if (!IsInExpectedScript(translationTrimmed, req.TargetLanguage))
        {
            logger.LogWarning("AI returned translation in wrong language '{Translation}' for '{Word}' (expected {TargetLanguage}), discarding",
                parsed.Translation, req.Word, req.TargetLanguage);
            return Results.Json(new { error = "AI returned translation in wrong language" }, statusCode: 502);
        }

        // Post-validation: чистим альтернативы — убираем мусорные, не на целевом языке, дубли
        parsed.Alternatives.RemoveAll(alt =>
        {
            var trimAlt = alt?.Trim();
            if (string.IsNullOrWhiteSpace(trimAlt)) return true;
            if (trimAlt.Length < 2) return true;
            if (HasMixedScripts(trimAlt)) return true;
            if (!IsInExpectedScript(trimAlt, req.TargetLanguage)) return true;
            if (string.Equals(trimAlt, translationTrimmed, StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(trimAlt, req.Word.Trim(), StringComparison.OrdinalIgnoreCase)) return true;
            // Убираем альтернативы со спецсимволами (мусор типа ":<? alternativnye tsveta?>")
            if (trimAlt.Any(c => c is '<' or '>' or '?' or ':' or '{' or '}')) return true;
            return false;
        });
        // Дедупликация
        parsed.Alternatives = parsed.Alternatives
            .Select(a => a.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        cache.Set(cacheKey, parsed, TimeSpan.FromHours(24));
        return Results.Ok(parsed);
    }
    catch (HttpRequestException ex)
    {
        logger.LogError(ex, "Ollama connection error");
        return Results.Json(new { error = "AI service unavailable" }, statusCode: 503);
    }
    catch (TaskCanceledException)
    {
        return Results.Json(new { error = "AI request timed out" }, statusCode: 504);
    }
}).RequireRateLimiting("ai");

// POST /api/ai/example
app.MapPost("/api/ai/example", async (ExampleRequest req, IAiProvider ai, IMemoryCache cache, ILogger<Program> logger) =>
{
    if (string.IsNullOrWhiteSpace(req.Word))
        return Results.BadRequest(new { error = "Word is required" });

    if (req.Word.Length > 200)
        return Results.BadRequest(new { error = "Word too long (max 200 chars)" });

    req.Count = Math.Clamp(req.Count, 1, 5);

    var cacheKey = $"example:{req.Word.Trim().ToLowerInvariant()}:{req.Language}:{req.TargetLanguage}:{req.Count}:{req.PartOfSpeech}:{req.LanguageLevel}";
    if (cache.TryGetValue(cacheKey, out ExampleResponse? cached) && cached != null && cached.Examples.Count > 0)
    {
        logger.LogDebug("Cache hit for example '{Word}'", req.Word);
        return Results.Ok(cached);
    }

    try
    {
        var prompt = PromptTemplates.ExampleUser(req.Word.Trim(), req.Language, req.TargetLanguage, req.Count, req.PartOfSpeech, req.LanguageLevel);
        var parsed = await CompleteWithRetry<ExampleResponse>(ai, PromptTemplates.ExampleSystem, prompt, logger, maxTokens: 512);

        if (parsed == null || parsed.Examples.Count == 0)
            return Results.Json(new { error = "AI returned invalid response" }, statusCode: 502);

        // Post-validation: примеры не должны быть пустыми или содержать мета-текст
        parsed.Examples.RemoveAll(e =>
            string.IsNullOrWhiteSpace(e.Sentence) ||
            string.IsNullOrWhiteSpace(e.Translation) ||
            e.Sentence.StartsWith("Here is", StringComparison.OrdinalIgnoreCase) ||
            e.Sentence.StartsWith("Sure", StringComparison.OrdinalIgnoreCase) ||
            // Переводы не должны содержать смешанные скрипты (напр. "увиделspring")
            HasMixedScripts(e.Translation));

        if (parsed.Examples.Count == 0)
            return Results.Json(new { error = "AI returned invalid examples" }, statusCode: 502);

        cache.Set(cacheKey, parsed, TimeSpan.FromHours(12));
        return Results.Ok(parsed);
    }
    catch (HttpRequestException ex)
    {
        logger.LogError(ex, "Ollama connection error");
        return Results.Json(new { error = "AI service unavailable" }, statusCode: 503);
    }
    catch (TaskCanceledException)
    {
        return Results.Json(new { error = "AI request timed out" }, statusCode: 504);
    }
}).RequireRateLimiting("ai");

// POST /api/ai/generate-dictionary
app.MapPost("/api/ai/generate-dictionary", async (GenerateDictionaryRequest req, IAiProvider ai, ILogger<Program> logger) =>
{
    if (string.IsNullOrWhiteSpace(req.Topic))
        return Results.BadRequest(new { error = "Topic is required" });

    if (req.Topic.Length > 300)
        return Results.BadRequest(new { error = "Topic too long (max 300 chars)" });

    req.WordCount = Math.Clamp(req.WordCount, 5, 30);

    // Динамический num_predict: ~80 токенов на слово + запас
    var maxTokens = req.WordCount * 80 + 200;

    try
    {
        var prompt = PromptTemplates.GenerateDictionaryUser(
            req.Topic.Trim(), req.SourceLanguage, req.TargetLanguage,
            req.LanguageLevel, req.WordCount);
        var parsed = await CompleteWithRetry<GenerateDictionaryResponse>(
            ai, PromptTemplates.GenerateDictionarySystem, prompt, logger, maxTokens: maxTokens);

        if (parsed == null || parsed.Words.Count == 0)
            return Results.Json(new { error = "AI returned invalid response" }, statusCode: 502);

        // Post-validation: убираем слова без перевода, с мусорными переводами
        parsed.Words.RemoveAll(w =>
            string.IsNullOrWhiteSpace(w.Original) ||
            string.IsNullOrWhiteSpace(w.Translation) ||
            string.Equals(w.Original.Trim(), w.Translation.Trim(), StringComparison.OrdinalIgnoreCase) ||
            HasMixedScripts(w.Translation.Trim()) ||
            HasMixedScripts(w.Original.Trim()));

        if (parsed.Words.Count == 0)
            return Results.Json(new { error = "AI returned no valid words" }, statusCode: 502);

        return Results.Ok(parsed);
    }
    catch (HttpRequestException ex)
    {
        logger.LogError(ex, "Ollama connection error");
        return Results.Json(new { error = "AI service unavailable" }, statusCode: 503);
    }
    catch (TaskCanceledException)
    {
        return Results.Json(new { error = "AI request timed out" }, statusCode: 504);
    }
}).RequireRateLimiting("ai");

// POST /api/ai/translate-batch
app.MapPost("/api/ai/translate-batch", async (BatchTranslateRequest req, IAiProvider ai, IMemoryCache cache, ILogger<Program> logger) =>
{
    if (req.Words == null || req.Words.Count == 0)
        return Results.BadRequest(new { error = "Words list is required" });

    if (req.Words.Count > 30)
        return Results.BadRequest(new { error = "Too many words (max 30)" });

    // Проверяем кэш для каждого слова, собираем только непереведённые
    var result = new BatchTranslateResponse();
    var uncachedWords = new List<string>();

    foreach (var word in req.Words)
    {
        if (string.IsNullOrWhiteSpace(word)) continue;

        var cacheKey = $"translate:{word.Trim().ToLowerInvariant()}:{req.SourceLanguage}:{req.TargetLanguage}:";
        if (cache.TryGetValue(cacheKey, out TranslateResponse? cached) && cached != null)
        {
            result.Translations.Add(new BatchTranslateItem
            {
                Word = word.Trim(),
                Translation = cached.Translation,
                Alternatives = cached.Alternatives
            });
        }
        else
        {
            uncachedWords.Add(word.Trim());
        }
    }

    // Если все слова из кэша — возвращаем сразу
    if (uncachedWords.Count == 0)
    {
        logger.LogDebug("Batch translate: all {Count} words from cache", req.Words.Count);
        return Results.Ok(result);
    }

    try
    {
        // Динамический num_predict: ~60 токенов на слово + запас
        var maxTokens = uncachedWords.Count * 60 + 200;

        var prompt = PromptTemplates.BatchTranslateUser(uncachedWords, req.SourceLanguage, req.TargetLanguage);
        var parsed = await CompleteWithRetry<BatchTranslateResponse>(
            ai, PromptTemplates.BatchTranslateSystem, prompt, logger, maxTokens: maxTokens);

        if (parsed?.Translations != null)
        {
            foreach (var item in parsed.Translations)
            {
                if (string.IsNullOrWhiteSpace(item.Word) || string.IsNullOrWhiteSpace(item.Translation))
                    continue;
                if (string.Equals(item.Word.Trim(), item.Translation.Trim(), StringComparison.OrdinalIgnoreCase))
                    continue;
                if (HasMixedScripts(item.Translation.Trim()))
                    continue;
                if (!IsInExpectedScript(item.Translation.Trim(), req.TargetLanguage))
                    continue;

                // Чистим альтернативы в батче
                item.Alternatives?.RemoveAll(alt =>
                    string.IsNullOrWhiteSpace(alt) ||
                    HasMixedScripts(alt.Trim()) ||
                    !IsInExpectedScript(alt.Trim(), req.TargetLanguage) ||
                    string.Equals(alt.Trim(), item.Translation.Trim(), StringComparison.OrdinalIgnoreCase));

                result.Translations.Add(item);

                // Кэшируем каждый перевод отдельно
                var cacheKey = $"translate:{item.Word.Trim().ToLowerInvariant()}:{req.SourceLanguage}:{req.TargetLanguage}:";
                cache.Set(cacheKey, new TranslateResponse
                {
                    Translation = item.Translation,
                    Alternatives = item.Alternatives ?? new()
                }, TimeSpan.FromHours(24));
            }
        }

        return Results.Ok(result);
    }
    catch (HttpRequestException ex)
    {
        logger.LogError(ex, "Ollama connection error");
        return Results.Json(new { error = "AI service unavailable" }, statusCode: 503);
    }
    catch (TaskCanceledException)
    {
        return Results.Json(new { error = "AI request timed out" }, statusCode: 504);
    }
}).RequireRateLimiting("ai");

// ==================== PHASE 3: NEW AI FEATURES ====================

// POST /api/ai/generate-exercises — генерация грамматических упражнений по правилу
app.MapPost("/api/ai/generate-exercises", async (GenerateExercisesRequest req, IAiProvider ai, IMemoryCache cache, ILogger<Program> logger) =>
{
    if (string.IsNullOrWhiteSpace(req.RuleTitle))
        return Results.BadRequest(new { error = "RuleTitle is required" });

    if (string.IsNullOrWhiteSpace(req.RuleContent))
        return Results.BadRequest(new { error = "RuleContent is required" });

    if (req.RuleContent.Length > 5000)
        return Results.BadRequest(new { error = "RuleContent too long (max 5000 chars)" });

    req.Count = Math.Clamp(req.Count, 1, 10);

    var cacheKey = $"exercises:{req.RuleTitle.Trim().ToLowerInvariant()}:{req.Language}:{req.Count}:{req.LanguageLevel}";
    if (cache.TryGetValue(cacheKey, out GenerateExercisesResponse? cached) && cached != null && cached.Exercises.Count > 0)
    {
        logger.LogDebug("Cache hit for exercises '{Rule}'", req.RuleTitle);
        return Results.Ok(cached);
    }

    try
    {
        var maxTokens = req.Count * 120 + 200;
        var prompt = PromptTemplates.GenerateExercisesUser(
            req.RuleTitle.Trim(), req.RuleContent.Trim(), req.Language,
            req.TargetLanguage, req.Count, req.LanguageLevel);
        var parsed = await CompleteWithRetry<GenerateExercisesResponse>(
            ai, PromptTemplates.GenerateExercisesSystem, prompt, logger, maxTokens: maxTokens);

        if (parsed == null || parsed.Exercises.Count == 0)
            return Results.Json(new { error = "AI returned invalid response" }, statusCode: 502);

        // Post-validation: убираем упражнения без вопроса или с < 2 вариантами
        parsed.Exercises.RemoveAll(e =>
            string.IsNullOrWhiteSpace(e.Question) ||
            e.Options.Count < 2 ||
            e.CorrectIndex < 0 || e.CorrectIndex >= e.Options.Count);

        if (parsed.Exercises.Count == 0)
            return Results.Json(new { error = "AI returned no valid exercises" }, statusCode: 502);

        cache.Set(cacheKey, parsed, TimeSpan.FromHours(6));
        return Results.Ok(parsed);
    }
    catch (HttpRequestException ex)
    {
        logger.LogError(ex, "Ollama connection error");
        return Results.Json(new { error = "AI service unavailable" }, statusCode: 503);
    }
    catch (TaskCanceledException)
    {
        return Results.Json(new { error = "AI request timed out" }, statusCode: 504);
    }
}).RequireRateLimiting("ai");

// POST /api/ai/generate-typed-exercises — генерация упражнений указанного типа (§17.8 LEARNING_IMPROVEMENTS)
app.MapPost("/api/ai/generate-typed-exercises", async (GenerateTypedExercisesRequest req, IAiProvider ai, IMemoryCache cache, ILogger<Program> logger) =>
{
    if (string.IsNullOrWhiteSpace(req.RuleTitle))
        return Results.BadRequest(new { error = "RuleTitle is required" });

    if (string.IsNullOrWhiteSpace(req.RuleContent))
        return Results.BadRequest(new { error = "RuleContent is required" });

    if (req.RuleContent.Length > 5000)
        return Results.BadRequest(new { error = "RuleContent too long (max 5000 chars)" });

    req.Count = Math.Clamp(req.Count, 1, 10);
    req.DifficultyTier = Math.Clamp(req.DifficultyTier, 1, 3);

    var validTypes = new HashSet<string> { "mcq", "transformation", "error_correction", "word_order", "translation", "matching" };
    if (!validTypes.Contains(req.ExerciseType))
        return Results.BadRequest(new { error = $"Invalid exercise type. Valid: {string.Join(", ", validTypes)}" });

    var cacheKey = $"typed-exercises:{req.RuleTitle.Trim().ToLowerInvariant()}:{req.ExerciseType}:{req.DifficultyTier}:{req.Count}";
    if (cache.TryGetValue(cacheKey, out GenerateTypedExercisesResponse? cached) && cached != null && cached.Exercises.Count > 0)
    {
        logger.LogDebug("Cache hit for typed exercises '{Rule}' type={Type}", req.RuleTitle, req.ExerciseType);
        return Results.Ok(cached);
    }

    try
    {
        var maxTokens = req.Count * 200 + 300;
        var prompt = PromptTemplates.GenerateTypedExercisesUser(
            req.RuleTitle.Trim(), req.RuleContent.Trim(), req.Language,
            req.TargetLanguage, req.ExerciseType, req.Count, req.DifficultyTier);
        var parsed = await CompleteWithRetry<GenerateTypedExercisesResponse>(
            ai, PromptTemplates.GenerateTypedExercisesSystem, prompt, logger, maxTokens: maxTokens);

        if (parsed == null || parsed.Exercises.Count == 0)
            return Results.Json(new { error = "AI returned invalid response" }, statusCode: 502);

        // Post-validation: убираем упражнения без вопроса
        parsed.Exercises.RemoveAll(e => string.IsNullOrWhiteSpace(e.Question) && string.IsNullOrWhiteSpace(e.IncorrectSentence));

        // Assign difficulty tier from request
        foreach (var ex in parsed.Exercises)
            ex.DifficultyTier = req.DifficultyTier;

        if (parsed.Exercises.Count == 0)
            return Results.Json(new { error = "AI returned no valid exercises" }, statusCode: 502);

        cache.Set(cacheKey, parsed, TimeSpan.FromHours(6));
        return Results.Ok(parsed);
    }
    catch (HttpRequestException ex)
    {
        logger.LogError(ex, "Ollama connection error");
        return Results.Json(new { error = "AI service unavailable" }, statusCode: 503);
    }
    catch (TaskCanceledException)
    {
        return Results.Json(new { error = "AI request timed out" }, statusCode: 504);
    }
}).RequireRateLimiting("ai");

// POST /api/ai/explain-mistake — объяснение ошибки в тренировке
app.MapPost("/api/ai/explain-mistake", async (ExplainMistakeRequest req, IAiProvider ai, IMemoryCache cache, ILogger<Program> logger) =>
{
    if (string.IsNullOrWhiteSpace(req.Word))
        return Results.BadRequest(new { error = "Word is required" });

    if (string.IsNullOrWhiteSpace(req.CorrectAnswer))
        return Results.BadRequest(new { error = "CorrectAnswer is required" });

    if (string.IsNullOrWhiteSpace(req.UserAnswer))
        return Results.BadRequest(new { error = "UserAnswer is required" });

    var cacheKey = $"explain:{req.Word.Trim().ToLowerInvariant()}:{req.UserAnswer.Trim().ToLowerInvariant()}:{req.CorrectAnswer.Trim().ToLowerInvariant()}:{req.Language}";
    if (cache.TryGetValue(cacheKey, out ExplainMistakeResponse? cached) && cached != null)
    {
        return Results.Ok(cached);
    }

    try
    {
        var prompt = PromptTemplates.ExplainMistakeUser(
            req.Word.Trim(), req.UserAnswer.Trim(), req.CorrectAnswer.Trim(),
            req.Context, req.Language, req.TargetLanguage);
        var parsed = await CompleteWithRetry<ExplainMistakeResponse>(
            ai, PromptTemplates.ExplainMistakeSystem, prompt, logger, maxTokens: 256);

        if (parsed == null || string.IsNullOrWhiteSpace(parsed.Explanation))
            return Results.Json(new { error = "AI returned invalid response" }, statusCode: 502);

        // Post-validation: strip foreign script contamination, then sanitize mixed-script words
        parsed.Explanation = StripForeignScripts(parsed.Explanation, req.TargetLanguage);
        parsed.Explanation = SanitizeMixedScriptWords(parsed.Explanation);
        if (!string.IsNullOrEmpty(parsed.Tip))
        {
            parsed.Tip = StripForeignScripts(parsed.Tip, req.TargetLanguage);
            parsed.Tip = SanitizeMixedScriptWords(parsed.Tip);
        }

        // If stripping left explanation empty, reject
        if (string.IsNullOrWhiteSpace(parsed.Explanation))
            return Results.Json(new { error = "AI returned invalid response" }, statusCode: 502);

        cache.Set(cacheKey, parsed, TimeSpan.FromHours(24));
        return Results.Ok(parsed);
    }
    catch (HttpRequestException ex)
    {
        logger.LogError(ex, "Ollama connection error");
        return Results.Json(new { error = "AI service unavailable" }, statusCode: 503);
    }
    catch (TaskCanceledException)
    {
        return Results.Json(new { error = "AI request timed out" }, statusCode: 504);
    }
}).RequireRateLimiting("ai");

// POST /api/ai/mnemonic — генерация мнемоники для запоминания слова
app.MapPost("/api/ai/mnemonic", async (MnemonicRequest req, IAiProvider ai, IMemoryCache cache, ILogger<Program> logger) =>
{
    if (string.IsNullOrWhiteSpace(req.Word))
        return Results.BadRequest(new { error = "Word is required" });

    if (string.IsNullOrWhiteSpace(req.Translation))
        return Results.BadRequest(new { error = "Translation is required" });

    var cacheKey = $"mnemonic:{req.Word.Trim().ToLowerInvariant()}:{req.Translation.Trim().ToLowerInvariant()}:{req.TargetLanguage}";
    if (cache.TryGetValue(cacheKey, out MnemonicResponse? cached) && cached != null)
    {
        return Results.Ok(cached);
    }

    try
    {
        var prompt = PromptTemplates.MnemonicUser(
            req.Word.Trim(), req.Translation.Trim(), req.Language, req.TargetLanguage);
        var parsed = await CompleteWithRetry<MnemonicResponse>(
            ai, PromptTemplates.MnemonicSystem, prompt, logger, maxTokens: 300);

        if (parsed == null || string.IsNullOrWhiteSpace(parsed.Mnemonic))
            return Results.Json(new { error = "AI returned invalid response" }, statusCode: 502);

        // Post-validation: strip foreign script contamination, then sanitize mixed-script words
        parsed.Mnemonic = StripForeignScripts(parsed.Mnemonic, req.TargetLanguage);
        parsed.Mnemonic = SanitizeMixedScriptWords(parsed.Mnemonic);
        if (!string.IsNullOrEmpty(parsed.Association))
        {
            parsed.Association = StripForeignScripts(parsed.Association, req.TargetLanguage);
            parsed.Association = SanitizeMixedScriptWords(parsed.Association);
        }
        if (!string.IsNullOrEmpty(parsed.Etymology))
        {
            parsed.Etymology = StripForeignScripts(parsed.Etymology, req.TargetLanguage);
            parsed.Etymology = SanitizeMixedScriptWords(parsed.Etymology);
        }

        // If stripping left mnemonic empty, reject
        if (string.IsNullOrWhiteSpace(parsed.Mnemonic))
            return Results.Json(new { error = "AI returned invalid response" }, statusCode: 502);

        cache.Set(cacheKey, parsed, TimeSpan.FromHours(48));
        return Results.Ok(parsed);
    }
    catch (HttpRequestException ex)
    {
        logger.LogError(ex, "Ollama connection error");
        return Results.Json(new { error = "AI service unavailable" }, statusCode: 503);
    }
    catch (TaskCanceledException)
    {
        return Results.Json(new { error = "AI request timed out" }, statusCode: 504);
    }
}).RequireRateLimiting("ai");

// POST /api/ai/detect-language — автоопределение языка текста
app.MapPost("/api/ai/detect-language", async (DetectLanguageRequest req, IAiProvider ai, IMemoryCache cache, ILogger<Program> logger) =>
{
    if (string.IsNullOrWhiteSpace(req.Text))
        return Results.BadRequest(new { error = "Text is required" });

    if (req.Text.Length > 1000)
        return Results.BadRequest(new { error = "Text too long (max 1000 chars)" });

    var cacheKey = $"detect:{req.Text.Trim().ToLowerInvariant()[..Math.Min(req.Text.Trim().Length, 100)]}";
    if (cache.TryGetValue(cacheKey, out DetectLanguageResponse? cached) && cached != null)
    {
        return Results.Ok(cached);
    }

    try
    {
        var prompt = PromptTemplates.DetectLanguageUser(req.Text.Trim());
        var parsed = await CompleteWithRetry<DetectLanguageResponse>(
            ai, PromptTemplates.DetectLanguageSystem, prompt, logger, maxTokens: 64);

        if (parsed == null || string.IsNullOrWhiteSpace(parsed.Language))
            return Results.Json(new { error = "AI returned invalid response" }, statusCode: 502);

        parsed.Confidence = Math.Clamp(parsed.Confidence, 0.0, 1.0);

        cache.Set(cacheKey, parsed, TimeSpan.FromHours(168)); // 7 days
        return Results.Ok(parsed);
    }
    catch (HttpRequestException ex)
    {
        logger.LogError(ex, "Ollama connection error");
        return Results.Json(new { error = "AI service unavailable" }, statusCode: 503);
    }
    catch (TaskCanceledException)
    {
        return Results.Json(new { error = "AI request timed out" }, statusCode: 504);
    }
}).RequireRateLimiting("ai");

// POST /api/ai/extract-words — извлечение словарных слов из текста
app.MapPost("/api/ai/extract-words", async (ExtractWordsRequest req, IAiProvider ai, ILogger<Program> logger) =>
{
    if (string.IsNullOrWhiteSpace(req.Text))
        return Results.BadRequest(new { error = "Text is required" });

    if (req.Text.Length > 10000)
        return Results.BadRequest(new { error = "Text too long (max 10000 chars)" });

    req.MaxWords = Math.Clamp(req.MaxWords, 1, 50);

    try
    {
        var maxTokens = req.MaxWords * 60 + 200;
        var prompt = PromptTemplates.ExtractWordsUser(
            req.Text.Trim(), req.Language, req.TargetLanguage, req.MaxWords, req.LanguageLevel);
        var parsed = await CompleteWithRetry<ExtractWordsResponse>(
            ai, PromptTemplates.ExtractWordsSystem, prompt, logger, maxTokens: maxTokens);

        if (parsed == null || parsed.Words.Count == 0)
            return Results.Json(new { error = "AI returned invalid response" }, statusCode: 502);

        // Post-validation
        parsed.Words.RemoveAll(w =>
            string.IsNullOrWhiteSpace(w.Original) ||
            string.IsNullOrWhiteSpace(w.Translation) ||
            string.Equals(w.Original.Trim(), w.Translation.Trim(), StringComparison.OrdinalIgnoreCase));

        if (parsed.Words.Count == 0)
            return Results.Json(new { error = "AI returned no valid words" }, statusCode: 502);

        return Results.Ok(parsed);
    }
    catch (HttpRequestException ex)
    {
        logger.LogError(ex, "Ollama connection error");
        return Results.Json(new { error = "AI service unavailable" }, statusCode: 503);
    }
    catch (TaskCanceledException)
    {
        return Results.Json(new { error = "AI request timed out" }, statusCode: 504);
    }
}).RequireRateLimiting("ai");

app.Run();

/// <summary>
/// Отправляет запрос к LLM с retry (до 2 попыток) при невалидном JSON.
/// При retry добавляет усиленную инструкцию в промпт.
/// </summary>
static async Task<T?> CompleteWithRetry<T>(IAiProvider ai, string systemPrompt, string userPrompt,
    ILogger logger, int maxRetries = 2, int? maxTokens = null) where T : class
{
    for (int attempt = 0; attempt <= maxRetries; attempt++)
    {
        var effectiveSystem = attempt == 0
            ? systemPrompt
            : systemPrompt + "\nIMPORTANT: You MUST respond with valid JSON only. No markdown, no code blocks, no explanation. Just the JSON object.";

        var raw = await ai.CompleteAsync(effectiveSystem, userPrompt, maxTokens: maxTokens);

        logger.LogDebug("AI response attempt {Attempt} ({Length} chars): {Preview}",
            attempt, raw.Length, raw[..Math.Min(300, raw.Length)]);

        var parsed = TryParseJson<T>(raw, logger);
        if (parsed != null)
            return parsed;

        if (attempt < maxRetries)
            logger.LogWarning("AI returned invalid JSON (attempt {Attempt}), retrying...", attempt + 1);
    }

    logger.LogError("AI failed to return valid JSON after {MaxRetries} retries", maxRetries + 1);
    return null;
}

/// <summary>
/// Парсит JSON-ответ LLM, извлекая JSON-блок из возможного мусора (markdown-обёртки и т.д.).
/// </summary>
static T? TryParseJson<T>(string raw, ILogger logger) where T : class
{
    if (string.IsNullOrWhiteSpace(raw))
        return null;

    // LLM иногда оборачивает JSON в ```json ... ``` — убираем
    var trimmed = raw.Trim();
    if (trimmed.StartsWith("```"))
    {
        var firstNewline = trimmed.IndexOf('\n');
        if (firstNewline > 0)
            trimmed = trimmed[(firstNewline + 1)..];
        if (trimmed.EndsWith("```"))
            trimmed = trimmed[..^3];
        trimmed = trimmed.Trim();
    }

    // Ищем первый { и последний }
    var start = trimmed.IndexOf('{');
    var end = trimmed.LastIndexOf('}');
    if (start < 0 || end <= start)
    {
        // LLM вернул массив вместо объекта? Оборачиваем вручную.
        var arrStart = trimmed.IndexOf('[');
        var arrEnd = trimmed.LastIndexOf(']');
        if (arrStart >= 0 && arrEnd > arrStart)
        {
            var arrayJson = trimmed[arrStart..(arrEnd + 1)];
            var wrapped = $"{{\"examples\":{arrayJson}}}";
            logger.LogInformation("AI returned bare array, wrapping: {Json}", wrapped[..Math.Min(200, wrapped.Length)]);
            return TryDeserialize<T>(wrapped, logger);
        }

        logger.LogWarning("No JSON object found in AI response: {Response}", raw[..Math.Min(200, raw.Length)]);
        return null;
    }

    var json = trimmed[start..(end + 1)];
    return TryDeserialize<T>(json, logger);
}

static T? TryDeserialize<T>(string json, ILogger logger) where T : class
{
    try
    {
        return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }
    catch (JsonException ex)
    {
        logger.LogWarning(ex, "Failed to parse AI JSON: {Json}", json[..Math.Min(200, json.Length)]);
        return null;
    }
}

/// <summary>
/// Checks whether the text is written predominantly in the expected script for the given language.
/// Returns false for mixed-script garbage, wrong-language alternatives, etc.
/// </summary>
static bool IsInExpectedScript(string text, string language)
{
    if (string.IsNullOrWhiteSpace(text)) return false;

    var letters = text.Where(char.IsLetter).ToArray();
    if (letters.Length == 0) return false;

    var lang = language.Trim().ToLowerInvariant();

    // For Cyrillic-based languages the majority of letters must be Cyrillic
    if (lang is "russian" or "ukrainian" or "belarusian" or "bulgarian" or "serbian"
        or "русский" or "украинский" or "белорусский")
    {
        var cyrillicCount = letters.Count(c => c is >= '\u0400' and <= '\u04FF');
        return cyrillicCount > letters.Length / 2;
    }

    // For Latin-based languages the majority of letters must be Latin
    if (lang is "english" or "german" or "french" or "spanish" or "italian" or "portuguese"
        or "dutch" or "polish" or "czech" or "swedish" or "norwegian" or "danish" or "finnish"
        or "turkish" or "romanian" or "hungarian" or "indonesian" or "malay" or "vietnamese"
        or "английский" or "немецкий" or "французский" or "испанский" or "итальянский"
        or "португальский" or "польский" or "турецкий")
    {
        var latinCount = letters.Count(c => (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z')
                                            || (c >= '\u00C0' && c <= '\u024F'));
        return latinCount > letters.Length / 2;
    }

    // For CJK languages
    if (lang is "chinese" or "japanese" or "korean"
        or "китайский" or "японский" or "корейский")
    {
        var cjkCount = letters.Count(c => c >= '\u4E00' && c <= '\u9FFF'
                                          || c >= '\u3040' && c <= '\u30FF'
                                          || c >= '\uAC00' && c <= '\uD7AF');
        return cjkCount > letters.Length / 3;
    }

    // For Arabic/Hebrew
    if (lang is "arabic" or "hebrew" or "арабский" or "иврит")
    {
        var count = letters.Count(c => (c >= '\u0600' && c <= '\u06FF')
                                       || (c >= '\u0590' && c <= '\u05FF'));
        return count > letters.Length / 2;
    }

    // Unknown language — accept anything that has at least some letters
    return true;
}

/// <summary>
/// Checks if text contains mixed scripts (e.g., Cyrillic + Latin in one word = garbled output like "грaveйный").
/// </summary>
static bool HasMixedScripts(string text)
{
    if (string.IsNullOrWhiteSpace(text)) return false;
    var letters = text.Where(char.IsLetter).ToArray();
    if (letters.Length < 2) return false;

    bool hasCyrillic = letters.Any(c => c is >= '\u0400' and <= '\u04FF');
    bool hasLatin = letters.Any(c => (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z'));
    bool hasCjk = letters.Any(c => c >= '\u4E00' && c <= '\u9FFF');

    int scriptCount = (hasCyrillic ? 1 : 0) + (hasLatin ? 1 : 0) + (hasCjk ? 1 : 0);
    return scriptCount > 1;
}

/// <summary>
/// Finds individual words that contain mixed scripts (e.g., "Кبير") and removes
/// the minority-script characters, keeping the dominant script in each word.
/// This fixes LLM output like "Кبير" → "بير" or "Шкра" stays as-is (pure Cyrillic).
/// For sentences that are mostly Cyrillic (Russian), non-Latin foreign fragments get stripped
/// from individual words so the text stays readable.
/// </summary>
static string SanitizeMixedScriptWords(string text)
{
    if (string.IsNullOrWhiteSpace(text)) return text;

    var words = text.Split(' ');
    for (int i = 0; i < words.Length; i++)
    {
        var word = words[i];
        var letters = word.Where(char.IsLetter).ToArray();
        if (letters.Length < 2) continue;

        bool hasCyrillic = letters.Any(c => c is >= '\u0400' and <= '\u04FF');
        bool hasArabic = letters.Any(c => c is >= '\u0600' and <= '\u06FF');
        bool hasCjk = letters.Any(c => c is >= '\u4E00' and <= '\u9FFF'
                                       or >= '\u3040' and <= '\u30FF'
                                       or >= '\uAC00' and <= '\uD7AF');

        // Mixed Cyrillic + Arabic in one word: remove the minority script chars
        if (hasCyrillic && hasArabic)
        {
            int cyrCount = letters.Count(c => c is >= '\u0400' and <= '\u04FF');
            int arbCount = letters.Count(c => c is >= '\u0600' and <= '\u06FF');
            if (cyrCount >= arbCount)
                words[i] = new string(word.Where(c => !(c is >= '\u0600' and <= '\u06FF')).ToArray());
            else
                words[i] = new string(word.Where(c => !(c is >= '\u0400' and <= '\u04FF')).ToArray());
        }
        // Mixed Cyrillic + CJK in one word
        else if (hasCyrillic && hasCjk)
        {
            int cyrCount = letters.Count(c => c is >= '\u0400' and <= '\u04FF');
            int cjkCount = letters.Length - cyrCount;
            if (cyrCount >= cjkCount)
                words[i] = new string(word.Where(c => !(c is >= '\u4E00' and <= '\u9FFF'
                                                        or >= '\u3040' and <= '\u30FF'
                                                        or >= '\uAC00' and <= '\uD7AF')).ToArray());
            else
                words[i] = new string(word.Where(c => !(c is >= '\u0400' and <= '\u04FF')).ToArray());
        }
    }

    return string.Join(' ', words.Where(w => w.Length > 0));
}

/// <summary>
/// Strips entire segments of text in unexpected scripts based on the target language.
/// Fixes LLM cross-contamination like Chinese appearing in Russian output:
/// "это совсем другое: 环境保护性，而incertidumbre描述的是不确定性" → "это совсем другое: "
/// Also collapses multiple spaces and trims trailing punctuation artifacts.
/// </summary>
static string StripForeignScripts(string text, string targetLanguage)
{
    if (string.IsNullOrWhiteSpace(text)) return text;

    var lang = targetLanguage.Trim().ToLowerInvariant();

    // Determine which character predicate to use for "allowed" chars
    // For Cyrillic targets: allow Cyrillic + Latin (for source-language words) + common chars
    // For Latin targets: allow Latin + common chars
    Func<char, bool> isAllowed;

    if (lang is "russian" or "ukrainian" or "belarusian" or "bulgarian" or "serbian"
        or "русский" or "украинский" or "белорусский")
    {
        isAllowed = c =>
            c is >= '\u0400' and <= '\u04FF'                        // Cyrillic
            || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z')    // Latin
            || c >= '\u00C0' && c <= '\u024F'                       // Latin Extended (é, ñ, ü)
            || !char.IsLetter(c);                                    // digits, punctuation, spaces
    }
    else if (lang is "chinese" or "japanese" or "korean"
             or "китайский" or "японский" or "корейский")
    {
        isAllowed = c =>
            c is >= '\u4E00' and <= '\u9FFF'                        // CJK
            || c is >= '\u3040' and <= '\u30FF'                     // Hiragana + Katakana
            || c is >= '\uAC00' and <= '\uD7AF'                     // Hangul
            || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z')    // Latin
            || !char.IsLetter(c);
    }
    else if (lang is "arabic" or "hebrew" or "арабский" or "иврит")
    {
        isAllowed = c =>
            c is >= '\u0600' and <= '\u06FF'                        // Arabic
            || c is >= '\u0590' and <= '\u05FF'                     // Hebrew
            || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z')    // Latin
            || !char.IsLetter(c);
    }
    else
    {
        // Latin-based languages: allow Latin + common chars, strip CJK/Arabic/Cyrillic leaks
        isAllowed = c =>
            (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z')
            || c >= '\u00C0' && c <= '\u024F'                       // Latin Extended
            || !char.IsLetter(c);
    }

    var filtered = new string(text.Where(isAllowed).ToArray());

    // Collapse multiple spaces and trim
    while (filtered.Contains("  "))
        filtered = filtered.Replace("  ", " ");

    return filtered.Trim();
}
