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

        // В вашем DataService
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
    }
}