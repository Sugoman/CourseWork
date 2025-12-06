namespace LearningTrainerShared.Models
{
    public class CreateDictionaryRequest
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string LanguageFrom { get; set; } = "English";
        public string LanguageTo { get; set; } = "Russian";
    }
}
