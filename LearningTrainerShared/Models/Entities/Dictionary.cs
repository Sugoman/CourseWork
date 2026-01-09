using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;


namespace LearningTrainerShared.Models
{
    public class Dictionary
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; }

        [MaxLength(500)]
        public string Description { get; set; }

        [MaxLength(50)]
        public string LanguageFrom { get; set; }

        [MaxLength(50)]
        public string LanguageTo { get; set; }

        public virtual ICollection<Word> Words { get; set; }
            = new System.Collections.ObjectModel.ObservableCollection<Word>();

        [JsonIgnore]
        public virtual User? User { get; set; }

        [NotMapped]
        public int WordCount => Words?.Count ?? 0;

        [JsonIgnore]
        public virtual ICollection<DictionarySharing> DictionarySharings { get; set; }
        = new List<DictionarySharing>();
    }
}