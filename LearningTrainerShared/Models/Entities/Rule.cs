using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace LearningTrainerShared.Models
{
    public class Rule
    {
        [Key]
        public int Id { get; set; }

        public int UserId { get; set; }

        [Required]
        [MaxLength(70)]
        public string Title { get; set; }
        
        [Required]
        public string MarkdownContent { get; set; }
        
        public string Description { get; set; }
        [MaxLength(50)]
        public string Category { get; set; } // "Grammar", "Vocabulary", "Pronunciation"
        
        public int DifficultyLevel { get; set; } = 1;

        // Marketplace fields
        public bool IsPublished { get; set; } = false;
        public double Rating { get; set; } = 0;
        public int RatingCount { get; set; } = 0;
        public int DownloadCount { get; set; } = 0;
        
        /// <summary>
        /// Ссылка на оригинальное правило, если это было скачано
        /// </summary>
        public int? SourceRuleId { get; set; }

        [JsonIgnore]
        public virtual User? User { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow; 
        [NotMapped]
        public bool IsFeatured { get; set; }
    }
}
