using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace LearningTrainerShared.Models
{
    public class Rule
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        [MaxLength(200)]
        public string Title { get; set; }
        
        [Required]
        public string MarkdownContent { get; set; }
        
        [MaxLength(50)]
        public string Description { get; set; }
        [MaxLength(50)]
        public string Category { get; set; } // "Grammar", "Vocabulary", "Pronunciation"
        
        public int DifficultyLevel { get; set; } = 1;

        [JsonIgnore]
        public virtual User User { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}