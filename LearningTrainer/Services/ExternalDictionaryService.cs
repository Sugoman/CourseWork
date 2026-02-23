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
            var details = await GetWordDetailsAsync(word);
            return details?.Transcription;
        }

        /// <summary>
        /// Возвращает транскрипцию и первый пример из dictionaryapi.dev.
        /// </summary>
        public async Task<WordDetailsResult?> GetWordDetailsAsync(string word)
        {
            if (string.IsNullOrWhiteSpace(word))
                return null;

            try
            {
                using var cts = new CancellationTokenSource(RequestTimeout);

                var response = await _httpClient.GetFromJsonAsync<List<DictionaryApiEntryDto>>(word, cts.Token);

                if (response != null && response.Count > 0)
                {
                    var entry = response[0];
                    var result = new WordDetailsResult();

                    // Транскрипция
                    if (entry.Phonetics != null && entry.Phonetics.Count > 0)
                    {
                        var phonetic = entry.Phonetics.FirstOrDefault(p => !string.IsNullOrEmpty(p.Text));
                        if (phonetic != null)
                            result.Transcription = phonetic.Text;
                    }
                    if (result.Transcription == null && !string.IsNullOrEmpty(entry.Phonetic))
                        result.Transcription = entry.Phonetic;

                    // Пример и определение из meanings
                    if (entry.Meanings != null)
                    {
                        foreach (var meaning in entry.Meanings)
                        {
                            if (meaning.Definitions == null) continue;
                            foreach (var def in meaning.Definitions)
                            {
                                if (result.Example == null && !string.IsNullOrEmpty(def.Example))
                                    result.Example = def.Example;
                                if (result.Definition == null && !string.IsNullOrEmpty(def.Definition))
                                    result.Definition = def.Definition;
                                if (result.Example != null && result.Definition != null)
                                    break;
                            }
                            if (result.Example != null && result.Definition != null)
                                break;
                        }
                    }

                    return result;
                }
            }
            catch (HttpRequestException ex)
            {
                if (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    System.Diagnostics.Debug.WriteLine($"Слово '{word}' не найдено в dictionaryapi.dev");
                    return null;
                }
                System.Diagnostics.Debug.WriteLine($"API dictionaryapi.dev error: {ex.Message}");
            }
            catch (TaskCanceledException)
            {
                System.Diagnostics.Debug.WriteLine($"Timeout getting details for '{word}'");
            }
            catch (OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine($"Timeout getting details for '{word}'");
            }

            return null;
        }
    }

    public class WordDetailsResult
    {
        public string? Transcription { get; set; }
        public string? Example { get; set; }
        public string? Definition { get; set; }
    }
}
