using LearningTrainerShared.Models;

namespace LearningTrainer.Services
{
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

    }
}
