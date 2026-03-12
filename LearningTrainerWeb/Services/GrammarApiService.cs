using System.Net.Http.Json;

namespace LearningTrainerWeb.Services;

/// <summary>
/// Сервис для работы с интерактивной грамматикой (§17 LEARNING_IMPROVEMENTS).
/// </summary>
public interface IGrammarApiService
{
    /// <summary>
    /// Получить дерево навыков с прогрессом пользователя.
    /// </summary>
    Task<List<SkillTreeNodeDto>> GetSkillTreeAsync();

    /// <summary>
    /// Получить правила, которые нужно повторить сегодня.
    /// </summary>
    Task<DueReviewsDto?> GetDueReviewsAsync();

    /// <summary>
    /// Получить детали правила: теория (HTML), прогресс, метаданные.
    /// </summary>
    Task<GrammarRuleDetailDto?> GetRuleDetailAsync(int ruleId);

    /// <summary>
    /// Начать сессию практики по правилу.
    /// </summary>
    Task<GrammarPracticeSession?> StartPracticeAsync(int ruleId, int count = 10);

    /// <summary>
    /// Отправить результат сессии.
    /// </summary>
    Task<GrammarSessionResultDto?> SubmitSessionAsync(int ruleId, GrammarSessionSubmission submission);

    /// <summary>
    /// Получить сводку прогресса по грамматике.
    /// </summary>
    Task<GrammarProgressSummaryDto?> GetProgressSummaryAsync();

    /// <summary>
    /// Сгенерировать и сохранить упражнения в банк правила через AI.
    /// </summary>
    Task<GeneratedGrammarExercisesResponseDto?> GenerateExercisesAsync(
        int ruleId,
        string exerciseType,
        int count = 5,
        int difficultyTier = 1);
}

public class GrammarApiService : IGrammarApiService
{
    private readonly HttpClient _httpClient;
    private readonly AuthTokenProvider _tokenProvider;
    private readonly ILogger<GrammarApiService> _logger;

    public GrammarApiService(HttpClient httpClient, AuthTokenProvider tokenProvider, ILogger<GrammarApiService> logger)
    {
        _httpClient = httpClient;
        _tokenProvider = tokenProvider;
        _logger = logger;
    }

    private async Task ApplyAuthAsync() => await _tokenProvider.EnsureValidTokenAsync(_httpClient);

    public async Task<List<SkillTreeNodeDto>> GetSkillTreeAsync()
    {
        try
        {
            await ApplyAuthAsync();
            return await _httpClient.GetFromJsonAsync<List<SkillTreeNodeDto>>("api/grammar/skill-tree")
                ?? new List<SkillTreeNodeDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching skill tree");
            return new List<SkillTreeNodeDto>();
        }
    }

    public async Task<DueReviewsDto?> GetDueReviewsAsync()
    {
        try
        {
            await ApplyAuthAsync();
            return await _httpClient.GetFromJsonAsync<DueReviewsDto>("api/grammar/due-reviews");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching due reviews");
            return null;
        }
    }

    public async Task<GrammarRuleDetailDto?> GetRuleDetailAsync(int ruleId)
    {
        try
        {
            await ApplyAuthAsync();
            return await _httpClient.GetFromJsonAsync<GrammarRuleDetailDto>($"api/grammar/{ruleId}/details");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching grammar rule detail {RuleId}", ruleId);
            return null;
        }
    }

    public async Task<GrammarPracticeSession?> StartPracticeAsync(int ruleId, int count = 10)
    {
        try
        {
            await ApplyAuthAsync();
            return await _httpClient.GetFromJsonAsync<GrammarPracticeSession>(
                $"api/grammar/{ruleId}/practice?count={count}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting grammar practice for rule {RuleId}", ruleId);
            return null;
        }
    }

    public async Task<GrammarSessionResultDto?> SubmitSessionAsync(int ruleId, GrammarSessionSubmission submission)
    {
        try
        {
            await ApplyAuthAsync();
            var response = await _httpClient.PostAsJsonAsync($"api/grammar/{ruleId}/submit-session", submission);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<GrammarSessionResultDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting grammar session for rule {RuleId}", ruleId);
            return null;
        }
    }

    public async Task<GrammarProgressSummaryDto?> GetProgressSummaryAsync()
    {
        try
        {
            await ApplyAuthAsync();
            return await _httpClient.GetFromJsonAsync<GrammarProgressSummaryDto>("api/grammar/progress-summary");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching grammar progress summary");
            return null;
        }
    }

    public async Task<GeneratedGrammarExercisesResponseDto?> GenerateExercisesAsync(
        int ruleId,
        string exerciseType,
        int count = 5,
        int difficultyTier = 1)
    {
        try
        {
            await ApplyAuthAsync();
            var url = $"api/grammar/{ruleId}/generate-exercises?type={Uri.EscapeDataString(exerciseType)}&count={count}&difficultyTier={difficultyTier}";
            var response = await _httpClient.PostAsync(url, content: null);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<GeneratedGrammarExercisesResponseDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error generating grammar exercises for rule {RuleId}, type {Type}, tier {Tier}",
                ruleId,
                exerciseType,
                difficultyTier);
            return null;
        }
    }
}

// === DTOs ===

public class SkillTreeNodeDto
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string? Category { get; set; }
    public int DifficultyLevel { get; set; }
    public int SkillTreeLevel { get; set; }
    public string? IconEmoji { get; set; }
    public string? SkillSummary { get; set; }
    public int XpReward { get; set; }
    public int ExerciseCount { get; set; }
    public int[] PrerequisiteRuleIds { get; set; } = Array.Empty<int>();
    public bool IsUnlocked { get; set; }
    public int KnowledgeLevel { get; set; }
    public DateTime? NextReview { get; set; }
    public int TotalSessions { get; set; }
    public double AccuracyPercent { get; set; }
    public DateTime? LastPracticeDate { get; set; }
}

public class DueReviewsDto
{
    public int Count { get; set; }
    public List<DueReviewRuleDto> Rules { get; set; } = new();
}

public class DueReviewRuleDto
{
    public int RuleId { get; set; }
    public string RuleTitle { get; set; } = "";
    public string? RuleCategory { get; set; }
    public string? IconEmoji { get; set; }
    public int KnowledgeLevel { get; set; }
    public DateTime? NextReview { get; set; }
    public int TotalSessions { get; set; }
    public double AccuracyPercent { get; set; }
    public int? OverdueDays { get; set; }
}

public class GrammarPracticeSession
{
    public int RuleId { get; set; }
    public string RuleTitle { get; set; } = "";
    public int KnowledgeLevel { get; set; }
    public List<GrammarPracticeExerciseDto> Exercises { get; set; } = new();
}

public class GrammarPracticeExerciseDto
{
    public int Id { get; set; }
    public string ExerciseType { get; set; } = "mcq";
    public string Question { get; set; } = "";
    public string? OptionsJson { get; set; }
    public int CorrectIndex { get; set; }
    public string? CorrectAnswer { get; set; }
    public string? AlternativeAnswersJson { get; set; }
    public string? IncorrectSentence { get; set; }
    public string? ShuffledWordsJson { get; set; }
    public string Explanation { get; set; } = "";
    public int DifficultyTier { get; set; }

    public string[] GetOptions()
    {
        if (string.IsNullOrEmpty(OptionsJson)) return Array.Empty<string>();
        try { return System.Text.Json.JsonSerializer.Deserialize<string[]>(OptionsJson) ?? Array.Empty<string>(); }
        catch { return Array.Empty<string>(); }
    }

    public string[] GetShuffledWords()
    {
        if (string.IsNullOrEmpty(ShuffledWordsJson)) return Array.Empty<string>();
        try { return System.Text.Json.JsonSerializer.Deserialize<string[]>(ShuffledWordsJson) ?? Array.Empty<string>(); }
        catch { return Array.Empty<string>(); }
    }

    public string[] GetAlternativeAnswers()
    {
        if (string.IsNullOrEmpty(AlternativeAnswersJson)) return Array.Empty<string>();
        try { return System.Text.Json.JsonSerializer.Deserialize<string[]>(AlternativeAnswersJson) ?? Array.Empty<string>(); }
        catch { return Array.Empty<string>(); }
    }
}

public class GrammarSessionSubmission
{
    public List<GrammarAnswerSubmission> Answers { get; set; } = new();
}

public class GrammarAnswerSubmission
{
    public int ExerciseId { get; set; }
    public string? UserAnswer { get; set; }
    public bool IsCorrect { get; set; }
    public int? ResponseTimeMs { get; set; }
}

public class GrammarSessionResultDto
{
    public int RuleId { get; set; }
    public int CorrectAnswers { get; set; }
    public int TotalAnswers { get; set; }
    public double AccuracyPercent { get; set; }
    public int KnowledgeLevel { get; set; }
    public int PreviousLevel { get; set; }
    public bool LevelUp { get; set; }
    public DateTime? NextReview { get; set; }
    public int XpEarned { get; set; }
}

public class GrammarProgressSummaryDto
{
    public int TotalRules { get; set; }
    public int StartedRules { get; set; }
    public int MasteredRules { get; set; }
    public int InProgressRules { get; set; }
    public int DueForReview { get; set; }
    public double OverallAccuracy { get; set; }
    public int TotalSessions { get; set; }
}

public class GrammarRuleDetailDto
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string? Category { get; set; }
    public int DifficultyLevel { get; set; }
    public int SkillTreeLevel { get; set; }
    public string? IconEmoji { get; set; }
    public string? SkillSummary { get; set; }
    public int XpReward { get; set; }
    public string HtmlContent { get; set; } = "";
    public int ExerciseCount { get; set; }
    // Progress fields
    public int KnowledgeLevel { get; set; }
    public double EaseFactor { get; set; }
    public double IntervalDays { get; set; }
    public DateTime? NextReview { get; set; }
    public int TotalSessions { get; set; }
    public int CorrectAnswers { get; set; }
    public int TotalAnswers { get; set; }
    public double AccuracyPercent { get; set; }
    public DateTime? LastPracticeDate { get; set; }
    public int LapseCount { get; set; }
}

public class GeneratedGrammarExercisesResponseDto
{
    public int RuleId { get; set; }
    public string RuleTitle { get; set; } = "";
    public string ExerciseType { get; set; } = "mcq";
    public int DifficultyTier { get; set; }
    public int RequestedCount { get; set; }
    public int GeneratedCount { get; set; }
    public int ReturnedCount { get; set; }
    public bool AiGenerationAttempted { get; set; }
    public bool AiGenerationSucceeded { get; set; }
    public bool AiServiceUnavailable { get; set; }
    public string? Warning { get; set; }
    public List<GeneratedGrammarExerciseDto> Exercises { get; set; } = new();
}

public class GeneratedGrammarExerciseDto
{
    public int Id { get; set; }
    public string ExerciseType { get; set; } = "mcq";
    public string Question { get; set; } = "";
    public string[] Options { get; set; } = Array.Empty<string>();
    public int CorrectIndex { get; set; }
    public string? CorrectAnswer { get; set; }
    public string[] AlternativeAnswers { get; set; } = Array.Empty<string>();
    public string? IncorrectSentence { get; set; }
    public string[] ShuffledWords { get; set; } = Array.Empty<string>();
    public string Explanation { get; set; } = "";
    public int DifficultyTier { get; set; }
    public int OrderIndex { get; set; }
}
