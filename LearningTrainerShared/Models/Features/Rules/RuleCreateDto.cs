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

        [MaxLength(50)]
        public string Description { get; set; }

        [MaxLength(50)]
        public string Category { get; set; }

        public int DifficultyLevel { get; set; } = 1;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public List<GrammarExerciseDto> Exercises { get; set; } = new();
    }

    public class GrammarExerciseDto
    {
        [Required]
        [MaxLength(500)]
        public string Question { get; set; } = "";

        public string[] Options { get; set; } = Array.Empty<string>();

        public int CorrectIndex { get; set; }

        [MaxLength(1000)]
        public string Explanation { get; set; } = "";

        public int OrderIndex { get; set; }
    }
}
