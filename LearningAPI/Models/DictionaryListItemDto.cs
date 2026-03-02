namespace LearningAPI.Services
{
    /// <summary>
    /// DTO for dictionary list items (replaces the hack of creating empty Word objects for WordCount).
    /// </summary>
    public class DictionaryListItemDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string LanguageFrom { get; set; } = "";
        public string LanguageTo { get; set; } = "";
        public bool IsPublished { get; set; }
        public double Rating { get; set; }
        public int RatingCount { get; set; }
        public int DownloadCount { get; set; }
        public int? SourceDictionaryId { get; set; }
        public int WordCount { get; set; }
        public string? Tags { get; set; }
    }
}
