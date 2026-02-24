using Ingat.AI.Models;
using Ingat.AI.Services;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient<IAiProvider, OllamaProvider>();

var app = builder.Build();

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
app.MapPost("/api/ai/translate", async (TranslateRequest req, IAiProvider ai, ILogger<Program> logger) =>
{
    if (string.IsNullOrWhiteSpace(req.Word))
        return Results.BadRequest(new { error = "Word is required" });

    if (req.Word.Length > 200)
        return Results.BadRequest(new { error = "Word too long (max 200 chars)" });

    try
    {
        var prompt = PromptTemplates.TranslateUser(req.Word.Trim(), req.SourceLanguage, req.TargetLanguage, req.Context, req.PartOfSpeech);
        var raw = await ai.CompleteAsync(PromptTemplates.TranslateSystem, prompt);

        var parsed = TryParseJson<TranslateResponse>(raw, logger);
        if (parsed == null)
            return Results.Json(new { error = "AI returned invalid response" }, statusCode: 502);

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
});

// POST /api/ai/example
app.MapPost("/api/ai/example", async (ExampleRequest req, IAiProvider ai, ILogger<Program> logger) =>
{
    if (string.IsNullOrWhiteSpace(req.Word))
        return Results.BadRequest(new { error = "Word is required" });

    if (req.Word.Length > 200)
        return Results.BadRequest(new { error = "Word too long (max 200 chars)" });

    req.Count = Math.Clamp(req.Count, 1, 5);

    try
    {
        var prompt = PromptTemplates.ExampleUser(req.Word.Trim(), req.Language, req.TargetLanguage, req.Count, req.PartOfSpeech, req.LanguageLevel);
        var raw = await ai.CompleteAsync(PromptTemplates.ExampleSystem, prompt);

        logger.LogInformation("AI example raw response for '{Word}': {Raw}", req.Word, raw[..Math.Min(500, raw.Length)]);

        var parsed = TryParseJson<ExampleResponse>(raw, logger);
        if (parsed == null || parsed.Examples.Count == 0)
            return Results.Json(new { error = "AI returned invalid response", rawPreview = raw[..Math.Min(300, raw.Length)] }, statusCode: 502);

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
});

app.Run();

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
