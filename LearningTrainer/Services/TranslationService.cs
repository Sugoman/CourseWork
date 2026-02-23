using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace LearningTrainer.Services
{
    /// <summary>
    /// Сервис автоперевода через MyMemory API (бесплатно, 5000 символов/день).
    /// </summary>
    public class TranslationService
    {
        private readonly HttpClient _httpClient;
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(5);

        public TranslationService()
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri("https://api.mymemory.translated.net/"),
                Timeout = RequestTimeout
            };
        }

        /// <summary>
        /// Переводит слово/фразу. langPair формат: "en|ru", "de|en", etc.
        /// </summary>
        public async Task<string?> TranslateAsync(string text, string langFrom, string langTo)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            try
            {
                var langPair = $"{MapLanguageCode(langFrom)}|{MapLanguageCode(langTo)}";
                using var cts = new CancellationTokenSource(RequestTimeout);

                var url = $"get?q={Uri.EscapeDataString(text)}&langpair={Uri.EscapeDataString(langPair)}";
                var response = await _httpClient.GetFromJsonAsync<MyMemoryResponse>(url, cts.Token);

                if (response?.ResponseData != null && response.ResponseStatus == 200)
                {
                    var translation = response.ResponseData.TranslatedText;
                    // MyMemory возвращает UPPERCASE если не уверен — приводим к нормальному виду
                    if (!string.IsNullOrEmpty(translation) && translation == translation.ToUpper() && translation.Length > 3)
                    {
                        translation = char.ToUpper(translation[0]) + translation.Substring(1).ToLower();
                    }
                    return translation;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Translation error: {ex.Message}");
            }

            return null;
        }

        private static string MapLanguageCode(string language)
        {
            return language?.ToLower() switch
            {
                "english" => "en",
                "russian" => "ru",
                "german" => "de",
                "french" => "fr",
                "spanish" => "es",
                "italian" => "it",
                "portuguese" => "pt",
                "chinese" => "zh",
                "japanese" => "ja",
                "korean" => "ko",
                "turkish" => "tr",
                "arabic" => "ar",
                "polish" => "pl",
                "dutch" => "nl",
                "swedish" => "sv",
                "czech" => "cs",
                "ukrainian" => "uk",
                _ => language?.ToLower() ?? "en"
            };
        }

        private class MyMemoryResponse
        {
            [JsonPropertyName("responseData")]
            public MyMemoryResponseData? ResponseData { get; set; }

            [JsonPropertyName("responseStatus")]
            public int ResponseStatus { get; set; }
        }

        private class MyMemoryResponseData
        {
            [JsonPropertyName("translatedText")]
            public string? TranslatedText { get; set; }
        }
    }
}
