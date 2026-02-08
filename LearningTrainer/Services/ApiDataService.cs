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
        
        // Marketplace publishing methods
        public async Task<bool> PublishDictionaryAsync(int dictionaryId)
        {
            try
            {
                var response = await _httpClient.PostAsync($"/api/marketplace/dictionaries/{dictionaryId}/publish", null);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PUBLISH] Dictionary publish error: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> UnpublishDictionaryAsync(int dictionaryId)
        {
            try
            {
                var response = await _httpClient.PostAsync($"/api/marketplace/dictionaries/{dictionaryId}/unpublish", null);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UNPUBLISH] Dictionary unpublish error: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> PublishRuleAsync(int ruleId)
        {
            try
            {
                var response = await _httpClient.PostAsync($"/api/marketplace/rules/{ruleId}/publish", null);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PUBLISH] Rule publish error: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> UnpublishRuleAsync(int ruleId)
        {
            try
            {
                var response = await _httpClient.PostAsync($"/api/marketplace/rules/{ruleId}/unpublish", null);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UNPUBLISH] Rule unpublish error: {ex.Message}");
                return false;
            }
        }

        // Export methods using ExportController
        public async Task<byte[]> ExportDictionaryAsJsonAsync(int dictionaryId)
        {
            var response = await _httpClient.GetAsync($"/api/dictionaries/export/{dictionaryId}/json");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsByteArrayAsync();
        }

        public async Task<byte[]> ExportDictionaryAsCsvAsync(int dictionaryId)
        {
            var response = await _httpClient.GetAsync($"/api/dictionaries/export/{dictionaryId}/csv");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsByteArrayAsync();
        }

        public async Task<byte[]> ExportAllDictionariesAsZipAsync()
        {
            var response = await _httpClient.GetAsync("/api/dictionaries/export/all/zip");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsByteArrayAsync();
        }

        #region Marketplace - Public Content

        public async Task<PagedResult<MarketplaceDictionaryItem>> GetPublicDictionariesAsync(
            string? search, string? languageFrom, string? languageTo, int page, int pageSize)
        {
            try
            {
                var url = $"/api/marketplace/dictionaries?page={page}&pageSize={pageSize}";
                if (!string.IsNullOrEmpty(search)) url += $"&search={Uri.EscapeDataString(search)}";
                if (!string.IsNullOrEmpty(languageFrom)) url += $"&languageFrom={languageFrom}";
                if (!string.IsNullOrEmpty(languageTo)) url += $"&languageTo={languageTo}";

                var result = await _httpClient.GetFromJsonAsync<PagedResult<MarketplaceDictionaryItem>>(url, _jsonOptions);
                return result ?? new PagedResult<MarketplaceDictionaryItem>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MARKETPLACE] GetPublicDictionaries error: {ex.Message}");
                return new PagedResult<MarketplaceDictionaryItem>();
            }
        }

        public async Task<PagedResult<MarketplaceRuleItem>> GetPublicRulesAsync(
            string? search, string? category, int difficulty, int page, int pageSize)
        {
            try
            {
                var url = $"/api/marketplace/rules?page={page}&pageSize={pageSize}";
                if (!string.IsNullOrEmpty(search)) url += $"&search={Uri.EscapeDataString(search)}";
                if (!string.IsNullOrEmpty(category)) url += $"&category={category}";
                if (difficulty > 0) url += $"&difficulty={difficulty}";

                var result = await _httpClient.GetFromJsonAsync<PagedResult<MarketplaceRuleItem>>(url, _jsonOptions);
                return result ?? new PagedResult<MarketplaceRuleItem>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MARKETPLACE] GetPublicRules error: {ex.Message}");
                return new PagedResult<MarketplaceRuleItem>();
            }
        }

        public async Task<MarketplaceDictionaryDetails?> GetMarketplaceDictionaryDetailsAsync(int id)
        {
            try
            {
                return await _httpClient.GetFromJsonAsync<MarketplaceDictionaryDetails>(
                    $"/api/marketplace/dictionaries/{id}", _jsonOptions);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MARKETPLACE] GetDictionaryDetails error: {ex.Message}");
                return null;
            }
        }

        public async Task<MarketplaceRuleDetails?> GetMarketplaceRuleDetailsAsync(int id)
        {
            try
            {
                return await _httpClient.GetFromJsonAsync<MarketplaceRuleDetails>(
                    $"/api/marketplace/rules/{id}", _jsonOptions);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MARKETPLACE] GetRuleDetails error: {ex.Message}");
                return null;
            }
        }

        public async Task<List<MarketplaceRuleItem>> GetRelatedRulesAsync(int ruleId, string category)
        {
            try
            {
                var result = await _httpClient.GetFromJsonAsync<List<MarketplaceRuleItem>>(
                    $"/api/marketplace/rules/{ruleId}/related?category={Uri.EscapeDataString(category)}", _jsonOptions);
                return result ?? new List<MarketplaceRuleItem>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MARKETPLACE] GetRelatedRules error: {ex.Message}");
                return new List<MarketplaceRuleItem>();
            }
        }

        #endregion

        #region Marketplace - Comments

        public async Task<List<CommentItem>> GetDictionaryCommentsAsync(int id)
        {
            try
            {
                var result = await _httpClient.GetFromJsonAsync<List<CommentItem>>(
                    $"/api/marketplace/dictionaries/{id}/comments", _jsonOptions);
                return result ?? new List<CommentItem>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[COMMENTS] GetDictionaryComments error: {ex.Message}");
                return new List<CommentItem>();
            }
        }

        public async Task<List<CommentItem>> GetRuleCommentsAsync(int id)
        {
            try
            {
                var result = await _httpClient.GetFromJsonAsync<List<CommentItem>>(
                    $"/api/marketplace/rules/{id}/comments", _jsonOptions);
                return result ?? new List<CommentItem>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[COMMENTS] GetRuleComments error: {ex.Message}");
                return new List<CommentItem>();
            }
        }

        public async Task<bool> AddDictionaryCommentAsync(int dictionaryId, int rating, string text)
        {
            try
            {
                var request = new { Rating = rating, Text = text };
                var response = await _httpClient.PostAsJsonAsync(
                    $"/api/marketplace/dictionaries/{dictionaryId}/comments", request, _jsonOptions);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[COMMENTS] AddDictionaryComment error: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> AddRuleCommentAsync(int ruleId, int rating, string text)
        {
            try
            {
                var request = new { Rating = rating, Text = text };
                var response = await _httpClient.PostAsJsonAsync(
                    $"/api/marketplace/rules/{ruleId}/comments", request, _jsonOptions);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[COMMENTS] AddRuleComment error: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> HasUserReviewedDictionaryAsync(int dictionaryId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"/api/marketplace/dictionaries/{dictionaryId}/has-reviewed");
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<HasReviewedResponse>(_jsonOptions);
                    return result?.HasReviewed ?? false;
                }
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[COMMENTS] HasUserReviewedDictionary error: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> HasUserReviewedRuleAsync(int ruleId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"/api/marketplace/rules/{ruleId}/has-reviewed");
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<HasReviewedResponse>(_jsonOptions);
                    return result?.HasReviewed ?? false;
                }
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[COMMENTS] HasUserReviewedRule error: {ex.Message}");
                return false;
            }
        }

        private class HasReviewedResponse { public bool HasReviewed { get; set; } }

        #endregion

        #region Marketplace - Download Content

        public async Task<(bool Success, string Message, int? NewId)> DownloadDictionaryFromMarketplaceAsync(int dictionaryId)
        {
            try
            {
                var response = await _httpClient.PostAsync($"/api/marketplace/dictionaries/{dictionaryId}/download", null);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<DownloadResponseDto>(_jsonOptions);
                    return (true, result?.Message ?? "Успешно", result?.NewDictionaryId);
                }
                return (false, "Ошибка скачивания", null);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DOWNLOAD] DownloadDictionary error: {ex.Message}");
                return (false, ex.Message, null);
            }
        }

        public async Task<(bool Success, string Message, int? NewId)> DownloadRuleFromMarketplaceAsync(int ruleId)
        {
            try
            {
                var response = await _httpClient.PostAsync($"/api/marketplace/rules/{ruleId}/download", null);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<DownloadResponseDto>(_jsonOptions);
                    return (true, result?.Message ?? "Успешно", result?.NewRuleId);
                }
                return (false, "Ошибка скачивания", null);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DOWNLOAD] DownloadRule error: {ex.Message}");
                return (false, ex.Message, null);
            }
        }

        public async Task<List<DownloadedItem>> GetDownloadedContentAsync()
        {
            try
            {
                var result = await _httpClient.GetFromJsonAsync<List<DownloadedItem>>(
                    "/api/marketplace/my/downloads", _jsonOptions);
                return result ?? new List<DownloadedItem>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DOWNLOAD] GetDownloadedContent error: {ex.Message}");
                return new List<DownloadedItem>();
            }
        }

        private class DownloadResponseDto
        {
            public string? Message { get; set; }
            public int? NewDictionaryId { get; set; }
            public int? NewRuleId { get; set; }
        }

        #endregion

        #region Training - Extended

        public async Task<DailyPlanDto?> GetDailyPlanAsync(int newWordsLimit = 10, int reviewLimit = 20)
        {
            try
            {
                return await _httpClient.GetFromJsonAsync<DailyPlanDto>(
                    $"/api/training/daily-plan?newWordsLimit={newWordsLimit}&reviewLimit={reviewLimit}", _jsonOptions);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TRAINING] GetDailyPlan error: {ex.Message}");
                return null;
            }
        }

        public async Task<List<TrainingWordDto>> GetTrainingWordsAsync(string mode, int? dictionaryId = null, int limit = 20)
        {
            try
            {
                var url = $"/api/training/words?mode={mode}&limit={limit}";
                if (dictionaryId.HasValue)
                    url += $"&dictionaryId={dictionaryId.Value}";

                var result = await _httpClient.GetFromJsonAsync<List<TrainingWordDto>>(url, _jsonOptions);
                return result ?? new List<TrainingWordDto>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TRAINING] GetTrainingWords error: {ex.Message}");
                return new List<TrainingWordDto>();
            }
        }

        public async Task<StarterPackResult?> InstallStarterPackAsync()
        {
            try
            {
                var response = await _httpClient.PostAsync("/api/training/starter-pack", null);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<StarterPackResult>(_jsonOptions);
                }
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TRAINING] InstallStarterPack error: {ex.Message}");
                return null;
            }
        }

        #endregion

        private class ApiResponseDto { public string Message { get; set; } }
    }
}
