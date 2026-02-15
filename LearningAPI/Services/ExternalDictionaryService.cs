using LearningTrainerShared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;

namespace LearningTrainer.Services
{
    public class ExternalDictionaryService
    {
        private readonly HttpClient _httpClient;

        /// <summary>
        /// Жёсткий таймаут на один запрос к внешнему API.
        /// </summary>
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(5);

        public ExternalDictionaryService(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _httpClient.BaseAddress = new Uri("https://api.dictionaryapi.dev/api/v2/entries/en/");
            _httpClient.Timeout = RequestTimeout;
        }

        public async Task<string?> GetTranscriptionAsync(string word)
        {
            if (string.IsNullOrWhiteSpace(word))
                return null;

            try
            {
                using var cts = new CancellationTokenSource(RequestTimeout);

                // https://api.dictionaryapi.dev/api/v2/entries/en/hello
                var response = await _httpClient.GetFromJsonAsync<List<DictionaryApiEntryDto>>(word, cts.Token);

                if (response != null && response.Count > 0)
                {
                    var entry = response[0];

                    if (entry.Phonetics != null && entry.Phonetics.Count > 0)
                    {
                        var phonetic = entry.Phonetics.FirstOrDefault(p => !string.IsNullOrEmpty(p.Text));
                        if (phonetic != null)
                        {
                            return phonetic.Text; // "həˈləʊ"
                        }
                    }

                    if (!string.IsNullOrEmpty(entry.Phonetic))
                    {
                        return entry.Phonetic;
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                // 404 (Not Found) (слово не найдено)
                if (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    System.Diagnostics.Debug.WriteLine($"Слово '{word}' не найдено в dictionaryapi.dev");
                    return null;
                }
                System.Diagnostics.Debug.WriteLine($"API dictionaryapi.dev error: {ex.Message}");
            }
            catch (TaskCanceledException)
            {
                System.Diagnostics.Debug.WriteLine($"Timeout getting transcription for '{word}'");
            }
            catch (OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine($"Timeout getting transcription for '{word}'");
            }

            return null;
        }
    }
}
