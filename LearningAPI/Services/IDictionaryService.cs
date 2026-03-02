using LearningTrainerShared.Models;

namespace LearningAPI.Services
{
    public interface IDictionaryService
    {
        Task<(List<DictionaryListItemDto> Data, int Total)> GetDictionariesPagedAsync(
            int userId, int page, int pageSize, string orderBy, bool descending, CancellationToken ct = default);

        Task<Dictionary?> GetByIdAsync(int userId, int dictionaryId, CancellationToken ct = default);
        Task<List<Dictionary>> GetAvailableAsync(int userId, CancellationToken ct = default);
        Task<Dictionary> CreateAsync(int userId, CreateDictionaryRequest request, CancellationToken ct = default);
        Task<bool> UpdateAsync(int userId, int dictionaryId, UpdateDictionaryRequest request, CancellationToken ct = default);
        Task<bool> DeleteAsync(int userId, int dictionaryId, CancellationToken ct = default);
        Task<List<Word>> GetReviewSessionAsync(int userId, int dictionaryId, CancellationToken ct = default);
        Task<List<string>> GetAllTagsAsync(int userId, CancellationToken ct = default);
    }
}
