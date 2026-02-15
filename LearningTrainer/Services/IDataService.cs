using LearningTrainerShared.Models;

namespace LearningTrainer.Services
{
    #region Marketplace DTOs

    public class PagedResult<T>
    {
        public List<T> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
        public int CurrentPage { get; set; }
    }

    public class MarketplaceDictionaryItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string LanguageFrom { get; set; } = "";
        public string LanguageTo { get; set; } = "";
        public int WordCount { get; set; }
        public string AuthorName { get; set; } = "";
        public double Rating { get; set; }
        public int Downloads { get; set; }
    }

    public class MarketplaceDictionaryDetails : MarketplaceDictionaryItem
    {
        public int RatingCount { get; set; }
        public int AuthorContentCount { get; set; }
        public List<WordPreview> PreviewWords { get; set; } = new();
    }

    public class WordPreview
    {
        public string Term { get; set; } = "";
        public string Translation { get; set; } = "";
    }

    public class MarketplaceRuleItem
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string Category { get; set; } = "";
        public int DifficultyLevel { get; set; }
        public string AuthorName { get; set; } = "";
        public double Rating { get; set; }
        public int Downloads { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class MarketplaceRuleDetails : MarketplaceRuleItem
    {
        public string HtmlContent { get; set; } = "";
        public int RatingCount { get; set; }
        public int AuthorContentCount { get; set; }
    }

    public class CommentItem
    {
        public int Id { get; set; }
        public string AuthorName { get; set; } = "";
        public int Rating { get; set; }
        public string Text { get; set; } = "";
        public DateTime CreatedAt { get; set; }
    }

    public class DownloadedItem
    {
        public int Id { get; set; }
        public string Type { get; set; } = "";
        public string Title { get; set; } = "";
        public string AuthorName { get; set; } = "";
        public DateTime DownloadedAt { get; set; }
    }

    public class StarterPackResult
    {
        public string Message { get; set; } = "";
        public int DictionaryId { get; set; }
        public int WordCount { get; set; }
    }

    #endregion

    public interface IDataService : IDisposable
    {
        Task<List<Dictionary>> GetDictionariesAsync();
        Task<List<Rule>> GetRulesAsync();
        Task<Dictionary> GetDictionaryByIdAsync(int id);
        Task<List<Word>> GetWordsByDictionaryAsync(int dictionaryId);
        Task InitializeTestDataAsync();
        Task WipeAndStoreDictionariesAsync(List<Dictionary> dictionariesFromServer);
        Task WipeAndStoreRulesAsync(List<Rule> rulesFromServer);
        Task<Dictionary> AddDictionaryAsync(Dictionary dictionary);
        Task<Word> AddWordAsync(Word word);
        Task<Rule> AddRuleAsync(Rule rule);
        Task<bool> DeleteWordAsync(int wordId);
        Task<bool> DeleteRuleAsync(int ruleId);
        Task<bool> DeleteDictionaryAsync(int dictionaryId);
        Task<bool> UpdateDictionaryAsync(Dictionary dictionary);
        Task<List<Word>> GetReviewSessionAsync(int dictionaryId);
        Task UpdateProgressAsync(UpdateProgressRequest progress);
        Task<string> ChangePasswordAsync(ChangePasswordRequest request);
        Task<string> RegisterAsync(RegisterRequest request); 
        Task<UserSessionDto> LoginAsync(object loginRequest);
        Task<UpgradeResultDto> UpgradeToTeacherAsync();
        Task<List<StudentDto>> GetMyStudentsAsync();
        Task<List<Dictionary>> GetAvailableDictionariesAsync();
        Task<List<Rule>> GetAvailableRulesAsync();
        Task<List<int>> GetDictionarySharingStatusAsync(int dictionaryId);
        Task<bool> UpdateRuleAsync(Rule rule);
        Task<SharingResultDto> ToggleDictionarySharingAsync(int dictionaryId, int studentId);
        Task<List<int>> GetRuleSharingStatusAsync(int ruleId);
        Task<SharingResultDto> ToggleRuleSharingAsync(int ruleId, int studentId);
        Task<DashboardStats> GetStatsAsync();
        void SetToken(string accessToken);
        
        // Marketplace publishing
        Task<bool> PublishDictionaryAsync(int dictionaryId);
        Task<bool> UnpublishDictionaryAsync(int dictionaryId);
        Task<bool> PublishRuleAsync(int ruleId);
        Task<bool> UnpublishRuleAsync(int ruleId);

        // Marketplace - Browse public content
        Task<PagedResult<MarketplaceDictionaryItem>> GetPublicDictionariesAsync(string? search, string? languageFrom, string? languageTo, int page, int pageSize);
        Task<PagedResult<MarketplaceRuleItem>> GetPublicRulesAsync(string? search, string? category, int difficulty, int page, int pageSize);
        Task<MarketplaceDictionaryDetails?> GetMarketplaceDictionaryDetailsAsync(int id);
        Task<MarketplaceRuleDetails?> GetMarketplaceRuleDetailsAsync(int id);
        Task<List<MarketplaceRuleItem>> GetRelatedRulesAsync(int ruleId, string category);

        // Marketplace - Comments
        Task<List<CommentItem>> GetDictionaryCommentsAsync(int id);
        Task<List<CommentItem>> GetRuleCommentsAsync(int id);
        Task<bool> AddDictionaryCommentAsync(int dictionaryId, int rating, string text);
        Task<bool> AddRuleCommentAsync(int ruleId, int rating, string text);
        Task<bool> HasUserReviewedDictionaryAsync(int dictionaryId);
        Task<bool> HasUserReviewedRuleAsync(int ruleId);

        // Marketplace - Download content
        Task<(bool Success, string Message, int? NewId)> DownloadDictionaryFromMarketplaceAsync(int dictionaryId);
        Task<(bool Success, string Message, int? NewId)> DownloadRuleFromMarketplaceAsync(int ruleId);
        Task<List<DownloadedItem>> GetDownloadedContentAsync();

        // Training - Extended
        Task<DailyPlanDto?> GetDailyPlanAsync(int newWordsLimit = 10, int reviewLimit = 20);
        Task<List<TrainingWordDto>> GetTrainingWordsAsync(string mode, int? dictionaryId = null, int limit = 20);
        Task<StarterPackResult?> InstallStarterPackAsync();

        // Export
        Task<byte[]> ExportDictionaryAsJsonAsync(int dictionaryId);
        Task<byte[]> ExportDictionaryAsCsvAsync(int dictionaryId);
        Task<byte[]> ExportAllDictionariesAsZipAsync();

        // Statistics
        Task<LearningTrainerShared.Models.Statistics.UserStatistics?> GetStatisticsAsync(string period = "week");
        Task SaveTrainingSessionAsync(DateTime startedAt, DateTime completedAt, int wordsReviewed, int correctAnswers, int wrongAnswers, string mode, int? dictionaryId);
    }
}
