namespace LearningTrainerShared.Models
{
    public class UpdateWordRequest
    {
        public int Id { get; set; }
        public string OriginalWord { get; set; } = "";
        public string Translation { get; set; } = "";
        public string Example { get; set; } = "";
    }
}
