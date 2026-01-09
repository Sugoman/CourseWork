using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace LearningTrainerShared.Models
{
    public class Word
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        [MaxLength(100)]
        public string OriginalWord { get; set; }

        [Required]
        [MaxLength(100)]
        public string Translation { get; set; }

        [MaxLength(500)]
        public string Example { get; set; }

        public int DictionaryId { get; set; }

        public DateTime AddedAt { get; set; } = DateTime.UtcNow;

        [JsonIgnore]
        public Dictionary Dictionary { get; set; }

        [JsonIgnore]
        public virtual User User { get; set; }

        [JsonIgnore]
        public virtual ICollection<LearningProgress> Progress { get; set; }

        public string? Transcription { get; set; }
    }
}
