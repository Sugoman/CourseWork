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
            e.Sentence.StartsWith("Here is", StringComparison.OrdinalIgnoreCase) ||
            e.Sentence.StartsWith("Sure", StringComparison.OrdinalIgnoreCase));

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

        // Post-validation: убираем слова без перевода
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
