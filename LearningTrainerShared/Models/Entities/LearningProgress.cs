using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace LearningTrainerShared.Models
{
    public class LearningProgress
    {
        [Key]
        public int Id { get; set; }

        public int UserId { get; set; }
        [JsonIgnore]
        public User User { get; set; }

        public int WordId { get; set; }
        [JsonIgnore]
        public Word Word { get; set; }

        public int CorrectAnswers { get; set; }
        public int TotalAttempts { get; set; }

        [Range(0, 10)]
        public int KnowledgeLevel { get; set; } // 0-10: уровень знания (repetition count в SM-2)

        /// <summary>
        /// Фактор лёгкости (SM-2 EaseFactor). Минимум 1.3, начальное значение 2.5.
        /// Определяет, насколько быстро растут интервалы повторения.
        /// </summary>
        public double EaseFactor { get; set; } = 2.5;

        /// <summary>
        /// Текущий интервал повторения в днях.
        /// </summary>
        public double IntervalDays { get; set; }

        public DateTime LastPracticed { get; set; }
        public DateTime NextReview { get; set; }

        [NotMapped]
        public double SuccessRate => TotalAttempts > 0 ? (double)CorrectAnswers / TotalAttempts : 0;
    }
}
