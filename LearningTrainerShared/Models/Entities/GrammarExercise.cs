using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace LearningTrainerShared.Models
{
    public class GrammarExercise
    {
        [Key]
        public int Id { get; set; }

        public int RuleId { get; set; }

        /// <summary>
        /// Тип упражнения: "mcq", "transformation", "error_correction",
        /// "word_order", "translation", "matching", "dictation"
        /// </summary>
        [MaxLength(30)]
        public string ExerciseType { get; set; } = "mcq";

        /// <summary>
        /// Вопрос упражнения, например: «He ___ to school every day»
        /// </summary>
        [Required]
        [MaxLength(500)]
        public string Question { get; set; } = "";

        /// <summary>
        /// Варианты ответов в формате JSON: ["go", "goes", "going", "gone"]
        /// Используется для типов mcq, matching.
        /// </summary>
        [Required]
        public string OptionsJson { get; set; } = "[]";

        /// <summary>
        /// Индекс правильного ответа (0-based). Для типа mcq.
        /// </summary>
        public int CorrectIndex { get; set; }

        /// <summary>
        /// Правильный ответ в текстовом виде (для transformation, error_correction, translation).
        /// Для mcq используется CorrectIndex + OptionsJson.
        /// </summary>
        [MaxLength(500)]
        public string? CorrectAnswer { get; set; }

        /// <summary>
        /// Допустимые альтернативные ответы (JSON array).
        /// Для гибкой проверки: ["I have eaten", "I've eaten"]
        /// </summary>
        public string? AlternativeAnswersJson { get; set; }

        /// <summary>
        /// Предложение с ошибкой (для error_correction).
        /// </summary>
        [MaxLength(500)]
        public string? IncorrectSentence { get; set; }

        /// <summary>
        /// Перемешанные слова (JSON array, для word_order).
        /// </summary>
        public string? ShuffledWordsJson { get; set; }

        /// <summary>
        /// Объяснение правильного ответа, например: «Present Simple, 3rd person → goes»
        /// </summary>
        [MaxLength(1000)]
        public string Explanation { get; set; } = "";

        [JsonIgnore]
        public virtual Rule? Rule { get; set; }

        /// <summary>
        /// Порядок отображения упражнения внутри правила
        /// </summary>
        public int OrderIndex { get; set; }

        /// <summary>
        /// Уровень сложности упражнения (1–3) внутри правила.
        /// Адаптивная система показывает упражнения подходящего уровня.
        /// </summary>
        public int DifficultyTier { get; set; } = 1;

        // Helper properties (not mapped to DB)
        [NotMapped]
        public string[] Options
        {
            get
            {
                try
                {
                    return System.Text.Json.JsonSerializer.Deserialize<string[]>(OptionsJson) ?? Array.Empty<string>();
                }
                catch
                {
                    return Array.Empty<string>();
                }
            }
            set
            {
                OptionsJson = System.Text.Json.JsonSerializer.Serialize(value ?? Array.Empty<string>());
            }
        }

        [NotMapped]
        public string[] AlternativeAnswers
        {
            get
            {
                if (string.IsNullOrEmpty(AlternativeAnswersJson)) return Array.Empty<string>();
                try { return System.Text.Json.JsonSerializer.Deserialize<string[]>(AlternativeAnswersJson) ?? Array.Empty<string>(); }
                catch { return Array.Empty<string>(); }
            }
            set => AlternativeAnswersJson = System.Text.Json.JsonSerializer.Serialize(value ?? Array.Empty<string>());
        }

        [NotMapped]
        public string[] ShuffledWords
        {
            get
            {
                if (string.IsNullOrEmpty(ShuffledWordsJson)) return Array.Empty<string>();
                try { return System.Text.Json.JsonSerializer.Deserialize<string[]>(ShuffledWordsJson) ?? Array.Empty<string>(); }
                catch { return Array.Empty<string>(); }
            }
            set => ShuffledWordsJson = System.Text.Json.JsonSerializer.Serialize(value ?? Array.Empty<string>());
        }

        /// <summary>
        /// Проверяет текстовый ответ пользователя с учётом CorrectAnswer и AlternativeAnswers.
        /// Используется для типов transformation, error_correction, translation.
        /// </summary>
        public bool CheckTextAnswer(string userAnswer)
        {
            if (string.IsNullOrWhiteSpace(userAnswer)) return false;
            var trimmed = userAnswer.Trim();

            if (!string.IsNullOrEmpty(CorrectAnswer) &&
                string.Equals(trimmed, CorrectAnswer.Trim(), StringComparison.OrdinalIgnoreCase))
                return true;

            return AlternativeAnswers.Any(alt =>
                string.Equals(trimmed, alt.Trim(), StringComparison.OrdinalIgnoreCase));
        }
    }
}
