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

        [Range(0, 5)]
        public int KnowledgeLevel { get; set; } // 0-5: от "не знаю" до "отлично знаю"

        public DateTime LastPracticed { get; set; }
        public DateTime NextReview { get; set; }

        [NotMapped]
        public double SuccessRate => TotalAttempts > 0 ? (double)CorrectAnswers / TotalAttempts : 0;
    }
}