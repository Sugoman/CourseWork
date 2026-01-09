using System.ComponentModel.DataAnnotations;

namespace LearningTrainerShared.Models
{
    public class CreateDictionaryRequest
    {
        [Required(ErrorMessage = "Имя словаря обязательно")]
        [StringLength(100, MinimumLength = 1, 
            ErrorMessage = "Имя должно быть от 1 до 100 символов")]
        public string Name { get; set; }

        [StringLength(500, ErrorMessage = "Описание не должно превышать 500 символов")]
        public string Description { get; set; }

        [Required(ErrorMessage = "Исходный язык обязателен")]
        [StringLength(50)]
        public string LanguageFrom { get; set; } = "English";

        [Required(ErrorMessage = "Целевой язык обязателен")]
        [StringLength(50)]
        public string LanguageTo { get; set; } = "Russian";
    }
}
