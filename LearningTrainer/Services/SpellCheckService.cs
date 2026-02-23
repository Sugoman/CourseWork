using FuzzySharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LearningTrainer.Services
{
    public class SpellCheckService
    {
        private List<string> _dictionary = new();
        private string _loadedLanguage = "";

        private static readonly Dictionary<string, string> LanguageFileMap = new(StringComparer.OrdinalIgnoreCase)
        {
            ["english"] = "large_en.txt",
            ["en"] = "large_en.txt",
            ["german"] = "large_de.txt",
            ["de"] = "large_de.txt",
            ["french"] = "large_fr.txt",
            ["fr"] = "large_fr.txt",
            ["spanish"] = "large_es.txt",
            ["es"] = "large_es.txt",
            ["italian"] = "large_it.txt",
            ["it"] = "large_it.txt",
            ["portuguese"] = "large_pt.txt",
            ["pt"] = "large_pt.txt",
            ["russian"] = "large_ru.txt",
            ["ru"] = "large_ru.txt",
        };

        public SpellCheckService()
        {
            LoadDictionary("english");
        }

        public SpellCheckService(string language)
        {
            LoadDictionary(language);
        }

        /// <summary>
        /// Загружает словарь для указанного языка. Если файл не найден — загружает английский.
        /// </summary>
        public void LoadDictionary(string language)
        {
            if (string.Equals(_loadedLanguage, language, StringComparison.OrdinalIgnoreCase))
                return;

            _loadedLanguage = language ?? "english";
            _dictionary = new List<string>();

            try
            {
                string filePath = ResolveFilePath(language);
                if (File.Exists(filePath))
                {
                    _dictionary.AddRange(File.ReadLines(filePath));
                }
                else
                {
                    // Fallback to English
                    string fallback = "large_en.txt";
                    if (File.Exists(fallback))
                    {
                        _dictionary.AddRange(File.ReadLines(fallback));
                        _loadedLanguage = "english";
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load dictionary for '{language}': {ex.Message}");
            }
        }

        private static string ResolveFilePath(string language)
        {
            if (string.IsNullOrEmpty(language))
                return "large_en.txt";

            if (LanguageFileMap.TryGetValue(language, out var mapped))
                return mapped;

            return $"large_{language.ToLower()}.txt";
        }

        public bool HasDictionary => _dictionary.Count > 0;

        public string SuggestCorrection(string inputWord)
        {
            if (string.IsNullOrWhiteSpace(inputWord) || _dictionary.Count == 0 || inputWord.Length < 3)
            {
                return null;
            }

            string bestSuggestion = null;
            int bestScore = 0;

            int inputLength = inputWord.Length;

            var relevantDictionary = _dictionary
                .Where(w => Math.Abs(w.Length - inputLength) <= 2);

            foreach (var dictWord in relevantDictionary)
            {
                int score = Fuzz.Ratio(inputWord.ToLower(), dictWord.ToLower());

                if (score > bestScore)
                {
                    bestScore = score;
                    bestSuggestion = dictWord;
                }

                if (bestScore > 95)
                {
                    break;
                }
            }

            if (bestScore >= 50 && bestSuggestion != null && bestSuggestion.ToLower() != inputWord.ToLower())
            {
                return bestSuggestion;
            }

            return null;
        }

        /// <summary>
        /// Асинхронная версия SuggestCorrection — выполняется в фоновом потоке.
        /// </summary>
        public Task<string> SuggestCorrectionAsync(string inputWord, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(inputWord) || _dictionary.Count == 0 || inputWord.Length < 3)
                return Task.FromResult<string>(null);

            return Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();
                return SuggestCorrection(inputWord);
            }, ct);
        }
    }
}
