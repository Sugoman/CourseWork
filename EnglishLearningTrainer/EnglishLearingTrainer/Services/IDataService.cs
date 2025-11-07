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
        void SetToken(string accessToken);

    }
}
