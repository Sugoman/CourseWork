using LearningTrainerShared.Models;

namespace LearningAPI.Services
{
    public interface IDictionaryService
    {
        Task<(List<Dictionary> Data, int Total)> GetDictionariesPagedAsync(
            int userId, int page, int pageSize, string orderBy, bool descending);

        Task<Dictionary?> GetByIdAsync(int userId, int dictionaryId);
        Task<List<Dictionary>> GetAvailableAsync(int userId);
        Task<Dictionary> CreateAsync(int userId, CreateDictionaryRequest request);
        Task<bool> UpdateAsync(int userId, int dictionaryId, UpdateDictionaryRequest request);
        Task<bool> DeleteAsync(int userId, int dictionaryId);
        Task<List<Word>> GetReviewSessionAsync(int userId, int dictionaryId);
    }
}
