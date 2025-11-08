using LearningTrainerShared.Models;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace LearningTrainer.Services
{
    public class ApiDataService : IDataService
    {

        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions;

        public ApiDataService()
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri("http://localhost:5076")
            };

            _jsonOptions = new JsonSerializerOptions
            {
                ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.Preserve,
                PropertyNameCaseInsensitive = true
            };
        }

        public async Task<List<Dictionary>> GetDictionariesAsync()
        {
            return await _httpClient.GetFromJsonAsync<List<Dictionary>>("/api/dictionaries", _jsonOptions);
        }

        public async Task<Dictionary> GetDictionaryByIdAsync(int id)
        {
            return await _httpClient.GetFromJsonAsync<Dictionary>($"/api/dictionaries/{id}", _jsonOptions);
        }

        public async Task<Dictionary> AddDictionaryAsync(Dictionary dictionary)
        {
            var requestDto = new CreateDictionaryRequest
            {
                Name = dictionary.Name,
                Description = dictionary.Description,
                LanguageFrom = dictionary.LanguageFrom,
                LanguageTo = dictionary.LanguageTo
            };

            var response = await _httpClient.PostAsJsonAsync("/api/dictionaries", requestDto, _jsonOptions);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<Dictionary>(_jsonOptions);
        }

        public async Task<bool> DeleteDictionaryAsync(int dictionaryId)
        {
            var response = await _httpClient.DeleteAsync($"/api/dictionaries/{dictionaryId}");
            return response.IsSuccessStatusCode;
        }

        // Rules 

        public async Task<List<Rule>> GetRulesAsync()
        {
            // GET /api/rules
            return await _httpClient.GetFromJsonAsync<List<Rule>>("/api/rules", _jsonOptions);
        }

        public async Task<Rule> AddRuleAsync(Rule rule)
        {
            // POST /api/rules
            var response = await _httpClient.PostAsJsonAsync("/api/rules", rule, _jsonOptions);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<Rule>(_jsonOptions);
        }

        public async Task<bool> DeleteRuleAsync(int ruleId)
        {
            // DELETE /api/rules/5
            var response = await _httpClient.DeleteAsync($"/api/rules/{ruleId}");
            return response.IsSuccessStatusCode;
        }

        // Words 
        public async Task<Word> AddWordAsync(Word word)
        {
            var requestDto = new CreateWordRequest
            {
                OriginalWord = word.OriginalWord,
                Translation = word.Translation,
                Example = word.Example,
                DictionaryId = word.DictionaryId
            };

            var response = await _httpClient.PostAsJsonAsync("/api/words", requestDto, _jsonOptions);

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<Word>(_jsonOptions);
        }

        public async Task<bool> DeleteWordAsync(int wordId)
        {
            // DELETE /api/words/5
            var response = await _httpClient.DeleteAsync($"/api/words/{wordId}");
            return response.IsSuccessStatusCode;
        }

        public Task<List<Word>> GetWordsByDictionaryAsync(int dictionaryId)
        {
            throw new NotImplementedException("Онлайн-метод GetWordsByDictionaryAsync еще не создан");
        }

        public Task<bool> UpdateDictionaryAsync(Dictionary dictionary)
        {
            throw new NotImplementedException("Онлайн-метод UpdateDictionaryAsync еще не создан");
        }

        public Task InitializeTestDataAsync()
        {
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
        public void SetToken(string accessToken)
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        }

        public async Task<List<Word>> GetReviewSessionAsync(int dictionaryId)
        {
            // /api/dictionaries/5/review
            return await _httpClient.GetFromJsonAsync<List<Word>>(
                $"/api/dictionaries/{dictionaryId}/review", _jsonOptions);
        }

        public async Task UpdateProgressAsync(UpdateProgressRequest progress)
        {
            // POST /api/progress/update
            var response = await _httpClient.PostAsJsonAsync(
                "/api/progress/update", progress, _jsonOptions);

            response.EnsureSuccessStatusCode();
        }

        public async Task<string> ChangePasswordAsync(ChangePasswordRequest request)
        {
            var response = await _httpClient.PostAsJsonAsync(
                "/api/auth/change-password", request, _jsonOptions);

            var responseDto = await response.Content.ReadFromJsonAsync<ApiResponseDto>();

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(responseDto?.Message ?? "Ошибка смены пароля");
            }

            return responseDto?.Message ?? "Успешно!";
        }

        public async Task<string> RegisterAsync(RegisterRequest request)
        {
            var response = await _httpClient.PostAsJsonAsync(
                "/api/auth/register", request, _jsonOptions);

            var responseDto = await response.Content.ReadFromJsonAsync<ApiResponseDto>();

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(responseDto?.Message ?? "Ошибка регистрации");
            }

            return responseDto?.Message ?? "Успешно!";
        }

        public async Task<UserSessionDto> LoginAsync(object loginRequest)
        {
            HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
                "/api/auth/login", loginRequest, _jsonOptions);

            if (!response.IsSuccessStatusCode)
            {
                var errorResponse = await response.Content.ReadFromJsonAsync<ApiResponseDto>();
                throw new HttpRequestException(errorResponse?.Message ?? "Неверный логин или пароль");
            }

            return await response.Content.ReadFromJsonAsync<UserSessionDto>(_jsonOptions);
        }


        private class ApiResponseDto { public string Message { get; set; } }
    }
}