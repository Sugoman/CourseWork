using LearningTrainerShared.Models.Features.Ai;
using System.Net.Http.Json;
using System.Text.Json;

namespace LearningAPI.Services
{
    public interface IAiGrammarExerciseService
    {
        Task<AiGrammarGenerationResult> GenerateTypedExercisesAsync(
            string ruleTitle,
            string ruleContent,
            string exerciseType,
            int count,
            int difficultyTier,
            CancellationToken ct = default);
    }

    public class AiGrammarGenerationResult
    {
        public bool IsSuccess { get; set; }
        public bool IsServiceUnavailable { get; set; }
        public string? ErrorMessage { get; set; }
        public List<AiTypedExerciseResult> Exercises { get; set; } = new();
    }

    /// <summary>
    /// Прокси к Ingat.AI для генерации типизированных грамматических упражнений (§17.9.8).
    /// </summary>
    public class AiGrammarExerciseService : IAiGrammarExerciseService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<AiGrammarExerciseService> _logger;
        private readonly IConfiguration _configuration;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public AiGrammarExerciseService(
            HttpClient httpClient,
            ILogger<AiGrammarExerciseService> logger,
            IConfiguration configuration)
        {
            _httpClient = httpClient;
            _logger = logger;
            _configuration = configuration;
        }

        public async Task<AiGrammarGenerationResult> GenerateTypedExercisesAsync(
            string ruleTitle,
            string ruleContent,
            string exerciseType,
            int count,
            int difficultyTier,
            CancellationToken ct = default)
        {
            try
            {
                var language = _configuration["AiService:Language"] ?? "English";
                var targetLanguage = _configuration["AiService:TargetLanguage"] ?? "Russian";

                const int MaxContentLength = 4800;
                var trimmedContent = ruleContent.Length > MaxContentLength
                    ? ruleContent[..MaxContentLength]
                    : ruleContent;

                var payload = new
                {
                    ruleTitle,
                    ruleContent = trimmedContent,
                    language,
                    targetLanguage,
                    exerciseType,
                    count,
                    difficultyTier
                };

                var response = await _httpClient.PostAsJsonAsync("/api/ai/generate-typed-exercises", payload, ct);

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync(ct);
                    var serviceUnavailable = (int)response.StatusCode >= 500;
                    _logger.LogWarning(
                        "AI typed exercise generation failed for rule '{RuleTitle}' ({StatusCode}): {ErrorBody}",
                        ruleTitle,
                        response.StatusCode,
                        errorBody);

                    return new AiGrammarGenerationResult
                    {
                        IsSuccess = false,
                        IsServiceUnavailable = serviceUnavailable,
                        ErrorMessage = $"AI service returned {(int)response.StatusCode}",
                        Exercises = new List<AiTypedExerciseResult>()
                    };
                }

                var dto = await response.Content.ReadFromJsonAsync<TypedExercisesDto>(JsonOptions, ct);
                if (dto?.Exercises == null)
                {
                    return new AiGrammarGenerationResult
                    {
                        IsSuccess = true,
                        IsServiceUnavailable = false,
                        ErrorMessage = null,
                        Exercises = new List<AiTypedExerciseResult>()
                    };
                }

                var exercises = dto.Exercises
                    .Where(e => !string.IsNullOrWhiteSpace(e.Question) || !string.IsNullOrWhiteSpace(e.IncorrectSentence))
                    .Select(e => new AiTypedExerciseResult
                    {
                        Question = e.Question ?? string.Empty,
                        Options = e.Options,
                        CorrectIndex = e.CorrectIndex,
                        CorrectAnswer = e.CorrectAnswer,
                        AlternativeAnswers = e.AlternativeAnswers,
                        IncorrectSentence = e.IncorrectSentence,
                        ShuffledWords = e.ShuffledWords,
                        Explanation = e.Explanation ?? string.Empty,
                        DifficultyTier = difficultyTier
                    })
                    .ToList();

                return new AiGrammarGenerationResult
                {
                    IsSuccess = true,
                    IsServiceUnavailable = false,
                    ErrorMessage = null,
                    Exercises = exercises
                };
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "AI typed exercise generation threw an exception for rule '{RuleTitle}'", ruleTitle);
                return new AiGrammarGenerationResult
                {
                    IsSuccess = false,
                    IsServiceUnavailable = true,
                    ErrorMessage = "AI service is unreachable",
                    Exercises = new List<AiTypedExerciseResult>()
                };
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogWarning(ex, "AI typed exercise generation timed out for rule '{RuleTitle}'", ruleTitle);
                return new AiGrammarGenerationResult
                {
                    IsSuccess = false,
                    IsServiceUnavailable = true,
                    ErrorMessage = "AI service request timed out",
                    Exercises = new List<AiTypedExerciseResult>()
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AI typed exercise generation threw an unexpected exception for rule '{RuleTitle}'", ruleTitle);
                return new AiGrammarGenerationResult
                {
                    IsSuccess = false,
                    IsServiceUnavailable = false,
                    ErrorMessage = "Unexpected AI generation error",
                    Exercises = new List<AiTypedExerciseResult>()
                };
            }
        }

        private sealed class TypedExercisesDto
        {
            public List<TypedExerciseItemDto>? Exercises { get; set; }
        }

        private sealed class TypedExerciseItemDto
        {
            public string? Question { get; set; }
            public List<string>? Options { get; set; }
            public int? CorrectIndex { get; set; }
            public string? CorrectAnswer { get; set; }
            public List<string>? AlternativeAnswers { get; set; }
            public string? IncorrectSentence { get; set; }
            public List<string>? ShuffledWords { get; set; }
            public string? Explanation { get; set; }
        }
    }
}
