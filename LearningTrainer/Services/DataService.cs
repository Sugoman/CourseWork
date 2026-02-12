using LearningTrainer.Context;
using LearningTrainerShared.Models;
using Microsoft.EntityFrameworkCore;
using System.Drawing.Text;
using System.Net.Http;
using System.Net.Http.Json;

namespace LearningTrainer.Services
{
    public class DataService : IDataService
    {
        private readonly LocalDbContext _context;

        public DataService()
        {
            _context = new LocalDbContext();

            _context.Database.EnsureCreated();
        }

        public async Task<List<Dictionary>> GetDictionariesAsync()
        {
            await EnsureDatabaseReadyAsync();

            return await _context.Dictionaries
                .Include(d => d.Words)
                .ToListAsync();
        }

        public async Task<List<Rule>> GetRulesAsync()
        {
            await EnsureDatabaseReadyAsync();
            return await _context.Rules.ToListAsync();
        }

        public async Task<Dictionary> GetDictionaryByIdAsync(int id)
        {
            await EnsureDatabaseReadyAsync();
            return await _context.Dictionaries
                .Include(d => d.Words)
                .FirstOrDefaultAsync(d => d.Id == id);
        }

        public async Task<List<Word>> GetWordsByDictionaryAsync(int dictionaryId)
        {
            await EnsureDatabaseReadyAsync();
            return await _context.Words
                .Where(w => w.DictionaryId == dictionaryId)
                .ToListAsync();
        }

        public async Task InitializeTestDataAsync()
        {
            await EnsureDatabaseReadyAsync();

            var hasDictionaries = await _context.Dictionaries.AnyAsync();
            var hasRules = await _context.Rules.AnyAsync();

            System.Diagnostics.Debug.WriteLine($"Before init - Has dictionaries: {hasDictionaries}, Has rules: {hasRules}");

            if (!hasDictionaries)
            {
                System.Diagnostics.Debug.WriteLine("Creating test dictionaries...");

                var dictionaries = new List<Dictionary>
        {
            new Dictionary
            {
                Name = "A1: Beginner English",
                Description = "Базовые слова для начинающих",
                LanguageFrom = "English",
                LanguageTo = "Russian",
                Words = new List<Word>
                {
                    new Word { OriginalWord = "Apple", Translation = "Яблоко", Example = "" },
                    new Word { OriginalWord = "Book", Translation = "Книга", Example = "" },
                    new Word { OriginalWord = "Car", Translation = "Машина", Example = "" },
                    new Word { OriginalWord = "House", Translation = "Дом", Example = "" },
                    new Word { OriginalWord = "Tree", Translation = "Дерево", Example = "" }
                }
            },
            new Dictionary
            {
                Name = "IT Terminology",
                Description = "Слова для программистов",
                LanguageFrom = "English",
                LanguageTo = "Russian",
                Words = new List<Word>
                {
                    new Word { OriginalWord = "Variable", Translation = "Переменная", Example = "" },
                    new Word { OriginalWord = "Function", Translation = "Функция", Example = "" },
                    new Word { OriginalWord = "Compiler", Translation = "Компилятор", Example = "" },
                    new Word { OriginalWord = "Framework", Translation = "Фреймворк", Example = "" }
                }
            }
        };

                await _context.Dictionaries.AddRangeAsync(dictionaries);
                var saved = await _context.SaveChangesAsync();
                System.Diagnostics.Debug.WriteLine($"Saved {saved} entities");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Dictionaries already exist, skipping creation");
            }

            var finalCount = await _context.Dictionaries.CountAsync();
            var wordCount = await _context.Words.CountAsync();
            System.Diagnostics.Debug.WriteLine($"After init - Dictionaries: {finalCount}, Words: {wordCount}");
        }

        private async Task EnsureDatabaseReadyAsync()
        {
            await _context.Database.EnsureCreatedAsync();
        }

        public void Dispose()
        {
            _context?.Dispose();
        }

        public async Task<Rule> AddRuleAsync(Rule rule)
        {
            HttpClient _httpClient = new HttpClient();
            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/rules", rule);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"HTTP Error: {response.StatusCode}");
                    System.Diagnostics.Debug.WriteLine($"Error Content: {errorContent}");
                    throw new HttpRequestException($"Error: {response.StatusCode} - {errorContent}");
                }

                return await response.Content.ReadFromJsonAsync<Rule>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Exception in DataService: {ex.Message}");
                throw;
            }
        }

        public async Task<Dictionary> AddDictionaryAsync(Dictionary dictionary)
        {
            _context.Dictionaries.Add(dictionary);
            await _context.SaveChangesAsync();
            return dictionary;
        }

        public async Task<Word> AddWordAsync(Word word)
        {
            _context.Words.Add(word);
            await _context.SaveChangesAsync();
            return word;
        }

        public async Task<bool> DeleteWordAsync(int wordId)
        {
            var word = await _context.Words.FindAsync(wordId);
            if (word != null)
            {
                _context.Words.Remove(word);
                await _context.SaveChangesAsync();
                return true;
            }
            return false;
        }

        public async Task<bool> DeleteRuleAsync(int ruleId)
        {
            var rule = await _context.Rules.FindAsync(ruleId);
            if (rule != null)
            {
                _context.Rules.Remove(rule);
                await _context.SaveChangesAsync();
                return true;
            }
            return false;
        }
        public async Task<bool> UpdateDictionaryAsync(Dictionary dictionary)
        {
            var existingDictionary = await _context.Dictionaries.FindAsync(dictionary.Id);
            if (existingDictionary != null)
            {
                existingDictionary.Name = dictionary.Name;
                existingDictionary.Description = dictionary.Description;
                existingDictionary.LanguageFrom = dictionary.LanguageFrom;
                existingDictionary.LanguageTo = dictionary.LanguageTo;

                await _context.SaveChangesAsync();
                return true;
            }
            return false;
        }

        public async Task<bool> DeleteDictionaryAsync(int dictionaryId)
        {
            var dictionary = await _context.Dictionaries
                .Include(d => d.Words)
                .FirstOrDefaultAsync(d => d.Id == dictionaryId);

            if (dictionary != null)
            {
                _context.Dictionaries.Remove(dictionary);
                await _context.SaveChangesAsync();
                return true;
            }
            return false;
        }

        public Task WipeAndStoreDictionariesAsync(List<Dictionary> dictionariesFromServer)
        {
            throw new NotImplementedException();
        }

        public Task WipeAndStoreRulesAsync(List<Rule> rulesFromServer)
        {
            throw new NotImplementedException();
        }

        public void SetToken(string accessToken)
        {
            throw new NotImplementedException();
        }

        public Task<List<Word>> GetReviewSessionAsync(int dictionaryId)
        {
            throw new NotImplementedException();
        }

        public Task UpdateProgressAsync(UpdateProgressRequest progress)
        {
            throw new NotImplementedException();
        }

        public Task<string> ChangePasswordAsync(ChangePasswordRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<string> RegisterAsync(RegisterRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<UserSessionDto> LoginAsync(object loginRequest)
        {
            throw new NotImplementedException();
        }

        public Task<UpgradeResultDto> UpgradeToTeacherAsync()
        {
            throw new NotImplementedException();
        }

        public Task<List<StudentDto>> GetMyStudentsAsync()
        {
            throw new NotImplementedException();
        }

        public Task<List<int>> GetDictionarySharingStatusAsync(int dictionaryId)
        {
            throw new NotImplementedException();
        }

        public Task<SharingResultDto> ToggleDictionarySharingAsync(int dictionaryId, int studentId)
        {
            throw new NotImplementedException();
        }

        public Task<List<Dictionary>> GetAvailableDictionariesAsync()
        {
            throw new NotImplementedException();
        }

        public Task<List<Rule>> GetAvailableRulesAsync()
        {
            throw new NotImplementedException();
        }

        public Task<List<int>> GetRuleSharingStatusAsync(int ruleId)
        {
            throw new NotImplementedException();
        }

        public Task<SharingResultDto> ToggleRuleSharingAsync(int ruleId, int studentId)
        {
            throw new NotImplementedException();
        }

        public Task<bool> UpdateRuleAsync(Rule rule)
        {
            throw new NotImplementedException();
        }
        public async Task<DashboardStats> GetStatsAsync()
        {
            await EnsureDatabaseReadyAsync();

            var stats = new DashboardStats();

            stats.TotalDictionaries = await _context.Dictionaries.CountAsync();
            stats.TotalWords = await _context.Words.CountAsync();

            var progresses = _context.LearningProgresses.AsQueryable();

            stats.LearnedWords = await progresses.CountAsync(p => p.KnowledgeLevel >= 4);

            stats.AverageSuccessRate = await progresses
                .Where(p => p.TotalAttempts > 0)
                .Select(p => (double)p.CorrectAnswers / p.TotalAttempts)
                .DefaultIfEmpty(0)
                .AverageAsync();

            var today = DateTime.UtcNow.Date;
            var fromDate = today.AddDays(-6);

            var activity = await progresses
                .Where(p => p.LastPracticed >= fromDate)
                .GroupBy(p => p.LastPracticed.Date)
                .Select(g => new ActivityPoint
                {
                    Date = g.Key,
                    Reviewed = g.Count(),
                    Learned = g.Count(p => p.KnowledgeLevel >= 4)
                })
                .ToListAsync();

            stats.ActivityLast7Days = activity;

            var distribution = await progresses
                .GroupBy(p => p.KnowledgeLevel)
                .Select(g => new KnowledgeDistributionPoint
                {
                    Level = g.Key,
                    Count = g.Count()
                })
                .ToListAsync();

            stats.KnowledgeDistribution = distribution;

            return stats;
        }
        
        // Marketplace - недоступно в локальном режиме
        public Task<bool> PublishDictionaryAsync(int dictionaryId)
        {
            throw new NotImplementedException("Публикация недоступна в локальном режиме");
        }

        public Task<bool> UnpublishDictionaryAsync(int dictionaryId)
        {
            throw new NotImplementedException("Публикация недоступна в локальном режиме");
        }

        public Task<bool> PublishRuleAsync(int ruleId)
        {
            throw new NotImplementedException("Публикация недоступна в локальном режиме");
        }

        public Task<bool> UnpublishRuleAsync(int ruleId)
        {
            throw new NotImplementedException("Публикация недоступна в локальном режиме");
        }

        // Export methods
        public async Task<byte[]> ExportDictionaryAsJsonAsync(int dictionaryId)
        {
            var dictionary = await _context.Dictionaries
                .Include(d => d.Words)
                .FirstOrDefaultAsync(d => d.Id == dictionaryId);

            if (dictionary == null)
                throw new InvalidOperationException("Словарь не найден");

            var exportData = new
            {
                Name = dictionary.Name,
                Description = dictionary.Description,
                LanguageFrom = dictionary.LanguageFrom,
                LanguageTo = dictionary.LanguageTo,
                ExportDate = DateTime.UtcNow,
                Words = dictionary.Words.Select(w => new
                {
                    Original = w.OriginalWord,
                    Translation = w.Translation,
                    PartOfSpeech = w.Transcription,
                    Example = w.Example
                }).ToList()
            };

            var json = System.Text.Json.JsonSerializer.Serialize(exportData, new System.Text.Json.JsonSerializerOptions 
            { 
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
            return System.Text.Encoding.UTF8.GetBytes(json);
        }

        public async Task<byte[]> ExportDictionaryAsCsvAsync(int dictionaryId)
        {
            var dictionary = await _context.Dictionaries
                .Include(d => d.Words)
                .FirstOrDefaultAsync(d => d.Id == dictionaryId);

            if (dictionary == null)
                throw new InvalidOperationException("Словарь не найден");

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Original,Translation,Part of Speech,Example");

            foreach (var word in dictionary.Words)
            {
                sb.AppendLine($"\"{word.OriginalWord}\",\"{word.Translation}\",\"{word.Transcription ?? ""}\",\"{word.Example ?? ""}\"");
            }

            return System.Text.Encoding.UTF8.GetBytes(sb.ToString());
        }

        public async Task<byte[]> ExportAllDictionariesAsZipAsync()
        {
            var dictionaries = await _context.Dictionaries
                .Include(d => d.Words)
                .ToListAsync();

            if (!dictionaries.Any())
                throw new InvalidOperationException("Нет словарей для экспорта");

            using var memoryStream = new System.IO.MemoryStream();
            using (var archive = new System.IO.Compression.ZipArchive(memoryStream, System.IO.Compression.ZipArchiveMode.Create, true))
            {
                foreach (var dictionary in dictionaries)
                {
                    var exportData = new
                    {
                        Name = dictionary.Name,
                        Description = dictionary.Description,
                        LanguageFrom = dictionary.LanguageFrom,
                        LanguageTo = dictionary.LanguageTo,
                        ExportDate = DateTime.UtcNow,
                        Words = dictionary.Words.Select(w => new
                        {
                            Original = w.OriginalWord,
                            Translation = w.Translation,
                            PartOfSpeech = w.Transcription,
                            Example = w.Example
                        }).ToList()
                    };

                    var json = System.Text.Json.JsonSerializer.Serialize(exportData, new System.Text.Json.JsonSerializerOptions 
                    { 
                        WriteIndented = true,
                        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                    });

                    var entry = archive.CreateEntry($"{dictionary.Name}.json");
                    using var entryStream = entry.Open();
                    await entryStream.WriteAsync(System.Text.Encoding.UTF8.GetBytes(json));
                }
            }

            return memoryStream.ToArray();
        }

        #region Marketplace - недоступно в локальном режиме

        public Task<PagedResult<MarketplaceDictionaryItem>> GetPublicDictionariesAsync(string? search, string? languageFrom, string? languageTo, int page, int pageSize)
        {
            throw new NotImplementedException("Маркетплейс недоступен в локальном режиме");
        }

        public Task<PagedResult<MarketplaceRuleItem>> GetPublicRulesAsync(string? search, string? category, int difficulty, int page, int pageSize)
        {
            throw new NotImplementedException("Маркетплейс недоступен в локальном режиме");
        }

        public Task<MarketplaceDictionaryDetails?> GetMarketplaceDictionaryDetailsAsync(int id)
        {
            throw new NotImplementedException("Маркетплейс недоступен в локальном режиме");
        }

        public Task<MarketplaceRuleDetails?> GetMarketplaceRuleDetailsAsync(int id)
        {
            throw new NotImplementedException("Маркетплейс недоступен в локальном режиме");
        }

        public Task<List<MarketplaceRuleItem>> GetRelatedRulesAsync(int ruleId, string category)
        {
            return Task.FromResult(new List<MarketplaceRuleItem>());
        }

        public Task<List<CommentItem>> GetDictionaryCommentsAsync(int id)
        {
            return Task.FromResult(new List<CommentItem>());
        }

        public Task<List<CommentItem>> GetRuleCommentsAsync(int id)
        {
            return Task.FromResult(new List<CommentItem>());
        }

        public Task<bool> AddDictionaryCommentAsync(int dictionaryId, int rating, string text)
        {
            throw new NotImplementedException("Комментарии недоступны в локальном режиме");
        }

        public Task<bool> AddRuleCommentAsync(int ruleId, int rating, string text)
        {
            throw new NotImplementedException("Комментарии недоступны в локальном режиме");
        }

        public Task<bool> HasUserReviewedDictionaryAsync(int dictionaryId)
        {
            return Task.FromResult(false);
        }

        public Task<bool> HasUserReviewedRuleAsync(int ruleId)
        {
            return Task.FromResult(false);
        }

        public Task<(bool Success, string Message, int? NewId)> DownloadDictionaryFromMarketplaceAsync(int dictionaryId)
        {
            throw new NotImplementedException("Скачивание из маркетплейса недоступно в локальном режиме");
        }

        public Task<(bool Success, string Message, int? NewId)> DownloadRuleFromMarketplaceAsync(int ruleId)
        {
            throw new NotImplementedException("Скачивание из маркетплейса недоступно в локальном режиме");
        }

        public Task<List<DownloadedItem>> GetDownloadedContentAsync()
        {
            return Task.FromResult(new List<DownloadedItem>());
        }

        public Task<DailyPlanDto?> GetDailyPlanAsync(int newWordsLimit = 10, int reviewLimit = 20)
        {
            return Task.FromResult<DailyPlanDto?>(null);
        }

        public Task<List<TrainingWordDto>> GetTrainingWordsAsync(string mode, int? dictionaryId = null, int limit = 20)
        {
            return Task.FromResult(new List<TrainingWordDto>());
        }

        public Task<StarterPackResult?> InstallStarterPackAsync()
        {
            throw new NotImplementedException("Стартовый набор недоступен в локальном режиме");
        }

        public Task<LearningTrainerShared.Models.Statistics.UserStatistics?> GetStatisticsAsync(string period = "week")
        {
            // В локальном режиме возвращаем null - статистика недоступна
            return Task.FromResult<LearningTrainerShared.Models.Statistics.UserStatistics?>(null);
        }

        public Task SaveTrainingSessionAsync(DateTime startedAt, DateTime completedAt, int wordsReviewed, int correctAnswers, int wrongAnswers, string mode, int? dictionaryId)
        {
            // В локальном режиме не сохраняем сессии
            return Task.CompletedTask;
        }

        #endregion
    }
}
