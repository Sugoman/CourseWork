using System.ComponentModel.DataAnnotations;

namespace EnglishLearningTrainer.Models
{
    public class Rule
    {
        [Key]
        public int Id { get; set; }
        
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
        
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}