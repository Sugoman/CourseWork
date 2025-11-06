using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace EnglishLearningTrainer.Models
{
    public class Word
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string OriginalWord { get; set; }

        [Required]
        [MaxLength(100)]
        public string Translation { get; set; }

        [MaxLength(500)]
        public string Example { get; set; }

        public int DictionaryId { get; set; }

        public DateTime AddedAt { get; set; } = DateTime.Now;
        public Dictionary Dictionary { get; set; }

        public string? Transcription { get; set; }
    }
}
