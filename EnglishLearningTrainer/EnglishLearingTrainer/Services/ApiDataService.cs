using EnglishLearningTrainer.Models;
using EnglishLearningTrainer.Services; // Твой IDataService
using LearningTrainerShared.Models;   // Твой DTO
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;

namespace EnglishLearningTrainer.Services // (Или какой у тебя namespace)
{
    //
    // ЭТО "ОНЛАЙН-МОЗГ" ДЛЯ WPF
    // Он "звонит" в API по каждому чиху
    //
    public class ApiDataService : IDataService
    {
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions;

        public ApiDataService()
        {
            // Настраиваем "телефон" (как в LoginViewModel)
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri("http://localhost:5076")
            };

            // Настраиваем "упаковщик" (с фиксом циклов)
            _jsonOptions = new JsonSerializerOptions
            {
                ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.Preserve,
                PropertyNameCaseInsensitive = true
            };
        }

        // --- Dictionaries ---

        public async Task<List<Dictionary>> GetDictionariesAsync()
        {
            // "Звоним" в GET /api/dictionaries
            return await _httpClient.GetFromJsonAsync<List<Dictionary>>("/api/dictionaries", _jsonOptions);
        }

        public async Task<Dictionary> GetDictionaryByIdAsync(int id)
        {
            // "Звоним" в GET /api/dictionaries/5
            return await _httpClient.GetFromJsonAsync<Dictionary>($"/api/dictionaries/{id}", _jsonOptions);
        }

        public async Task<Dictionary> AddDictionaryAsync(Dictionary dictionary)
        {
            // --- АДАПТЕР ---
            // "Контракт" IDataService просит 'Dictionary',
            // а API ждёт 'CreateDictionaryRequest'.
            // Конвертируем "на лету":
            var requestDto = new CreateDictionaryRequest
            {
                Name = dictionary.Name,
                Description = dictionary.Description,
                LanguageFrom = dictionary.LanguageFrom,
                LanguageTo = dictionary.LanguageTo
            };

            // "Звоним" в POST /api/dictionaries
            var response = await _httpClient.PostAsJsonAsync("/api/dictionaries", requestDto, _jsonOptions);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<Dictionary>(_jsonOptions);
        }

        public async Task<bool> DeleteDictionaryAsync(int dictionaryId)
        {
            // "Звоним" в DELETE /api/dictionaries/5
            var response = await _httpClient.DeleteAsync($"/api/dictionaries/{dictionaryId}");
            return response.IsSuccessStatusCode;
        }

        // --- Rules ---

        public async Task<List<Rule>> GetRulesAsync()
        {
            // "Звоним" в GET /api/rules
            return await _httpClient.GetFromJsonAsync<List<Rule>>("/api/rules", _jsonOptions);
        }

        public async Task<Rule> AddRuleAsync(Rule rule)
        {
            // "Звоним" в POST /api/rules
            var response = await _httpClient.PostAsJsonAsync("/api/rules", rule, _jsonOptions);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<Rule>(_jsonOptions);
        }

        public async Task<bool> DeleteRuleAsync(int ruleId)
        {
            // "Звоним" в DELETE /api/rules/5
            var response = await _httpClient.DeleteAsync($"/api/rules/{ruleId}");
            return response.IsSuccessStatusCode;
        }

        // --- Words ---

        public async Task<Word> AddWordAsync(Word word)
        {
            // "Звоним" в POST /api/words
            var response = await _httpClient.PostAsJsonAsync("/api/words", word, _jsonOptions);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<Word>(_jsonOptions);
        }

        public async Task<bool> DeleteWordAsync(int wordId)
        {
            // "Звоним" в DELETE /api/words/5
            var response = await _httpClient.DeleteAsync($"/api/words/{wordId}");
            return response.IsSuccessStatusCode;
        }

        // --- Методы, которые мы пока не "опубликовали" в API ---
        // (Они нужны, чтобы IDataService не ругался)
        public Task<List<Word>> GetWordsByDictionaryAsync(int dictionaryId)
        {
            // TODO: "Опубликовать" в API метод /api/dictionaries/{id}/words
            throw new NotImplementedException("Онлайн-метод GetWordsByDictionaryAsync еще не создан");
        }

        public Task<bool> UpdateDictionaryAsync(Dictionary dictionary)
        {
            // TODO: "Опубликовать" в API метод PUT /api/dictionaries/{id}
            throw new NotImplementedException("Онлайн-метод UpdateDictionaryAsync еще не создан");
        }

        public Task InitializeTestDataAsync()
        {
            // Этот метод не нужен для API
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }

        public Task WipeAndStoreDictionariesAsync(List<Dictionary> dictionariesFromServer)
        {
            throw new NotImplementedException();
        }

        public Task WipeAndStoreRulesAsync(List<Rule> rulesFromServer)
        {
            throw new NotImplementedException();
        }
    }
}