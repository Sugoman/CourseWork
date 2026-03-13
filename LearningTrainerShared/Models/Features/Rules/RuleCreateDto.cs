using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LearningTrainerShared.Models
{
    public class RuleCreateDto
    {
        [Required]
        [MaxLength(200)]
        public string Title { get; set; }

        [Required]
        public string MarkdownContent { get; set; }

        [MaxLength(500)]
        public string Description { get; set; }

        [MaxLength(50)]
        public string Category { get; set; }

        public int DifficultyLevel { get; set; } = 1;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public List<GrammarExerciseDto> Exercises { get; set; } = new();
    }

    public class GrammarExerciseDto
    {
        [MaxLength(30)]
        public string ExerciseType { get; set; } = "mcq";

        [Required]
        [MaxLength(500)]
        public string Question { get; set; } = "";

        public string[] Options { get; set; } = Array.Empty<string>();

        public int CorrectIndex { get; set; }

        [MaxLength(500)]
        public string? CorrectAnswer { get; set; }

        public string? AlternativeAnswersJson { get; set; }

        [MaxLength(500)]
        public string? IncorrectSentence { get; set; }

        public string? ShuffledWordsJson { get; set; }

        [MaxLength(1000)]
        public string Explanation { get; set; } = "";

        public int OrderIndex { get; set; }

        public int DifficultyTier { get; set; } = 1;
    }
}
