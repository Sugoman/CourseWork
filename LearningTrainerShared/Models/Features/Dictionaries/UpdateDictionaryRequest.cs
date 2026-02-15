using System.ComponentModel.DataAnnotations;

namespace LearningTrainerShared.Models
{
    public class UpdateDictionaryRequest
    {
        [Required(ErrorMessage = "Dictionary ID is required")]
        public int Id { get; set; }

        [Required]
        [StringLength(100, MinimumLength = 1)]
        public string Name { get; set; } = "";

        [StringLength(500)]
        public string? Description { get; set; }

        [Required]
        [StringLength(50)]
        public string LanguageFrom { get; set; } = "English";

        [Required]
        [StringLength(50)]
        public string LanguageTo { get; set; } = "Russian";
    }
}
