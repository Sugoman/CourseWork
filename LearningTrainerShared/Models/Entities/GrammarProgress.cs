using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace LearningTrainerShared.Models
{
    /// <summary>
    /// Прогресс изучения грамматического правила (аналог LearningProgress для слов).
    /// Использует SM-2 для интервального повторения грамматики (§17.2 LEARNING_IMPROVEMENTS).
    /// </summary>
    public class GrammarProgress
    {
        [Key]
        public int Id { get; set; }

        public int UserId { get; set; }
        public int RuleId { get; set; }

        /// <summary>
        /// Уровень знания правила (0 = не начато, 1–5 = мастерство).
        /// Повышается при успешном прохождении упражнений, понижается при ошибках.
        /// </summary>
        [Range(0, 10)]
        public int KnowledgeLevel { get; set; }

        /// <summary>
        /// SM-2 ease factor для грамматического правила.
        /// </summary>
        public double EaseFactor { get; set; } = 2.5;

        /// <summary>
        /// Текущий интервал повторения в днях.
        /// </summary>
        public double IntervalDays { get; set; }

        /// <summary>
        /// Дата следующего повторения.
        /// </summary>
        public DateTime? NextReview { get; set; }

        /// <summary>
        /// Общее количество пройденных сессий упражнений по этому правилу.
        /// </summary>
        public int TotalSessions { get; set; }

        /// <summary>
        /// Количество правильных ответов из всех сессий.
        /// </summary>
        public int CorrectAnswers { get; set; }

        /// <summary>
        /// Общее количество ответов из всех сессий.
        /// </summary>
        public int TotalAnswers { get; set; }

        /// <summary>
        /// Дата последнего прохождения.
        /// </summary>
        public DateTime? LastPracticeDate { get; set; }

        /// <summary>
        /// Количество «провалов» (accuracy &lt; 50% за сессию).
        /// Используется для leech detection.
        /// </summary>
        public int LapseCount { get; set; }

        [JsonIgnore]
        [ForeignKey("UserId")]
        public virtual User? User { get; set; }

        [JsonIgnore]
        [ForeignKey("RuleId")]
        public virtual Rule? Rule { get; set; }

        /// <summary>
        /// Accuracy по всем сессиям.
        /// </summary>
        [NotMapped]
        public double AccuracyPercent => TotalAnswers > 0 ? (double)CorrectAnswers / TotalAnswers * 100 : 0;
    }
}
