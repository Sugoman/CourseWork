using LearningTrainerShared.Models;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text;
using System.Net.Http.Headers;

namespace LearningTrainer.Services
{

    public class SharingResultDto { public string Message { get; set; } public string Status { get; set; } }
    public class ApiDataService : IDataService
    {

        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly IConfiguration _configuration;

        public ApiDataService(IConfiguration configuration)
        {
            _configuration = configuration;
            _httpClient = new HttpClient();
            
            var apiBaseUrl = _configuration["Api:BaseUrl"] 
                ?? "http://localhost:5077";
            _httpClient.BaseAddress = new Uri(apiBaseUrl);

            _jsonOptions = new JsonSerializerOptions
            {
                ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.Preserve,
                PropertyNameCaseInsensitive = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
        }

        public void SetToken(string accessToken)
        {
            if (string.IsNullOrWhiteSpace(accessToken)) return;
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        private class PagedResponse<T>
        {
            public List<T>? data { get; set; }
        }

        public async Task<List<Dictionary>> GetDictionariesAsync()
        {
            var resp = await _httpClient.GetFromJsonAsync<PagedResponse<Dictionary>>("/api/dictionaries", _jsonOptions);
            return resp?.data ?? new List<Dictionary>();
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

        public async Task<bool> UpdateDictionaryAsync(Dictionary dictionary)
        {
            try
            {
                var response = await _httpClient.PutAsJsonAsync($"api/dictionaries/{dictionary.Id}", dictionary);

                if (response.IsSuccessStatusCode)
                {
                    return true;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"API ERROR [{response.StatusCode}]: {errorContent}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CLIENT EXCEPTION: {ex.Message}");
                return false;
            }
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
            var response = await _httpClient.PostAsJsonAsync("/api/auth/register", request, _jsonOptions);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();

                try
                {
                    var errorDto = System.Text.Json.JsonSerializer.Deserialize<ApiResponseDto>(errorContent, _jsonOptions);
                    if (!string.IsNullOrEmpty(errorDto?.Message))
                    {
                        throw new HttpRequestException(errorDto.Message);
                    }
                }
                catch (System.Text.Json.JsonException)
                {
                }

                throw new HttpRequestException($"Ошибка регистрации ({response.StatusCode}): {errorContent}");
            }

            var responseDto = await response.Content.ReadFromJsonAsync<ApiResponseDto>(_jsonOptions);
            return responseDto?.Message ?? "Успешно!";
        }

        public async Task<UserSessionDto> LoginAsync(object loginRequest)
        {
            HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
                "/api/auth/login", loginRequest, _jsonOptions);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                
                try
                {
                    var errorDto = System.Text.Json.JsonSerializer.Deserialize<ApiResponseDto>(errorContent, _jsonOptions);
                    if (!string.IsNullOrEmpty(errorDto?.Message))
                    {
                        throw new HttpRequestException(errorDto.Message);
                    }
                }
                catch (System.Text.Json.JsonException)
                {
                }
                
                throw new HttpRequestException(errorContent ?? "Неверный логин или пароль");
            }

            return await response.Content.ReadFromJsonAsync<UserSessionDto>(_jsonOptions);
        }

        public async Task<UpgradeResultDto> UpgradeToTeacherAsync()
        {
            var response = await _httpClient.PostAsync("/api/auth/upgrade-to-teacher", null);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                
                // Парсим JSON ответ для получения понятного сообщения
                string errorMessage;
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(errorContent);
                    errorMessage = doc.RootElement.TryGetProperty("message", out var msgElement) 
                        ? msgElement.GetString() 
                        : errorContent;
                }
                catch
                {
                    errorMessage = errorContent;
                }
                
                throw new HttpRequestException(errorMessage);
            }
            
            return await response.Content.ReadFromJsonAsync<UpgradeResultDto>(_jsonOptions);
        }

        public async Task<List<StudentDto>> GetMyStudentsAsync()
        {
            // GET /api/classroom/students
            return await _httpClient.GetFromJsonAsync<List<StudentDto>>("/api/classroom/students", _jsonOptions)
                ?? new List<StudentDto>();
        }

        public async Task<List<int>> GetDictionarySharingStatusAsync(int dictionaryId)
        {
            var result = await _httpClient.GetFromJsonAsync<List<int>>(
                $"/api/sharing/dictionary/{dictionaryId}/status",
                _jsonOptions
            );

            return result ?? new List<int>();
        }

        public async Task<SharingResultDto> ToggleDictionarySharingAsync(int dictionaryId, int studentId)
        {
            var request = new { ContentId = dictionaryId, StudentId = studentId };

            var response = await _httpClient.PostAsJsonAsync("/api/sharing/dictionary/toggle", request, _jsonOptions);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<SharingResultDto>(_jsonOptions)
                ?? new SharingResultDto();
        }


        public async Task<List<Dictionary>> GetAvailableDictionariesAsync()
        {

            return await _httpClient.GetFromJsonAsync<List<Dictionary>>("/api/dictionaries/list/available", _jsonOptions);
        }

        public async Task<List<Rule>> GetAvailableRulesAsync()
        {
            return await _httpClient.GetFromJsonAsync<List<Rule>>("/api/rules/list/available", _jsonOptions);
        }

        public async Task<List<int>> GetRuleSharingStatusAsync(int ruleId)
        {
            var result = await _httpClient.GetFromJsonAsync<List<int>>(
                $"/api/sharing/rule/{ruleId}/status",
                _jsonOptions
            );

            return result ?? new List<int>();
        }

        public async Task<SharingResultDto> ToggleRuleSharingAsync(int ruleId, int studentId)
        {
            var request = new { ContentId = ruleId, StudentId = studentId };
            var response = await _httpClient.PostAsJsonAsync("/api/sharing/rule/toggle", request, _jsonOptions);

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<SharingResultDto>(_jsonOptions)
                   ?? new SharingResultDto();
        }
        public async Task<bool> UpdateRuleAsync(Rule rule)
        {
            try
            {
                var response = await _httpClient.PutAsJsonAsync($"api/rules/{rule.Id}", rule, _jsonOptions);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EXCEPTION] Rule update error: {ex.Message}");
                return false;
            }
        }

        public async Task<DashboardStats> GetStatsAsync()
        {
            try
            {
                var stats = await _httpClient.GetFromJsonAsync<DashboardStats>("/api/progress/stats", _jsonOptions);
                return stats ?? new DashboardStats();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[STATS] Failed to load stats: {ex.Message}");
                return new DashboardStats();
            }
        }
        private class ApiResponseDto { public string Message { get; set; } }
    }
}