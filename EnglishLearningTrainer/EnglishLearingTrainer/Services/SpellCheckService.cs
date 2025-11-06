using FuzzySharp; // Наша библиотека
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace EnglishLearningTrainer.Services
{
    public class SpellCheckService
    {
        private readonly List<string> _dictionary;

        public SpellCheckService()
        {
            _dictionary = new List<string>();
            LoadDictionary();
        }

        private void LoadDictionary()
        {
            try
            {
                string filePath = "large_en.txt";
                if (File.Exists(filePath))
                {
                    _dictionary.AddRange(File.ReadLines(filePath));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load dictionary: {ex.Message}");
            }
        }

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

            if (bestScore >= 80 && bestSuggestion.ToLower() != inputWord.ToLower())
            {
                return bestSuggestion;
            }

            return null; 
        }
    }
}