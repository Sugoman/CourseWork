using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ingat.AI.Services;

/// <summary>
/// Реализация IAiProvider через Ollama HTTP API (/api/chat).
/// </summary>
public sealed class OllamaProvider : IAiProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _model;
    private readonly ILogger<OllamaProvider> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public OllamaProvider(HttpClient httpClient, IConfiguration config, ILogger<OllamaProvider> logger)
    {
        _httpClient = httpClient;
        _model = config["Ollama:Model"] ?? "qwen2.5:7b";
        _logger = logger;

        var baseUrl = config["Ollama:BaseUrl"] ?? "http://localhost:11434";
        _httpClient.BaseAddress = new Uri(baseUrl);

        var timeoutSeconds = config.GetValue<int?>("Ollama:TimeoutSeconds") ?? 120;
        _httpClient.Timeout = TimeSpan.FromSeconds(timeoutSeconds);

        _logger.LogInformation("Ollama configured: {BaseUrl}, model={Model}, timeout={Timeout}s",
            baseUrl, _model, timeoutSeconds);
    }

    public async Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default, int? maxTokens = null)
    {
        var request = new OllamaChatRequest
        {
            Model = _model,
            Stream = false,
            Messages = new List<OllamaMessage>
            {
                new() { Role = "system", Content = systemPrompt },
                new() { Role = "user",   Content = userPrompt }
            },
            Options = new OllamaOptions
            {
                Temperature = 0.3,
                NumPredict = maxTokens ?? 512
            }
        };

        _logger.LogDebug("Ollama request to model {Model}: {Prompt}", _model, userPrompt[..Math.Min(100, userPrompt.Length)]);

        var response = await _httpClient.PostAsJsonAsync("/api/chat", request, JsonOptions, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(JsonOptions, ct);
        var content = result?.Message?.Content ?? string.Empty;

        _logger.LogDebug("Ollama response ({Length} chars)", content.Length);
        return content;
    }

    #region Ollama DTOs

    private sealed class OllamaChatRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("messages")]
        public List<OllamaMessage> Messages { get; set; } = new();

        [JsonPropertyName("stream")]
        public bool Stream { get; set; }

        [JsonPropertyName("options")]
        public OllamaOptions? Options { get; set; }
    }

    private sealed class OllamaMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
    }

    private sealed class OllamaOptions
    {
        [JsonPropertyName("temperature")]
        public double Temperature { get; set; }

        [JsonPropertyName("num_predict")]
        public int NumPredict { get; set; }
    }

    private sealed class OllamaChatResponse
    {
        [JsonPropertyName("message")]
        public OllamaMessage? Message { get; set; }
    }

    #endregion
}
