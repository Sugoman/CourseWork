using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EnglishLearningTrainer.Models
{
    public class Dictionary
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; }

        [MaxLength(500)]
        public string Description { get; set; }

        [MaxLength(50)]
        public string LanguageFrom { get; set; }

        [MaxLength(50)]
        public string LanguageTo { get; set; }

        public virtual ICollection<Word> Words { get; set; } = new List<Word>();

        [NotMapped]
        public int WordCount => Words?.Count ?? 0;
    }
}