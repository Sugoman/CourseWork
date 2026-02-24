using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace LearningTrainerShared.Models
{
    public class GrammarExercise
    {
        [Key]
        public int Id { get; set; }

        public int RuleId { get; set; }

        /// <summary>
        /// Вопрос упражнения, например: «He ___ to school every day»
        /// </summary>
        [Required]
        [MaxLength(500)]
        public string Question { get; set; } = "";

        /// <summary>
        /// Варианты ответов в формате JSON: ["go", "goes", "going", "gone"]
        /// </summary>
        [Required]
        public string OptionsJson { get; set; } = "[]";

        /// <summary>
        /// Индекс правильного ответа (0-based)
        /// </summary>
        public int CorrectIndex { get; set; }

        /// <summary>
        /// Объяснение правильного ответа, например: «Present Simple, 3rd person → goes»
        /// </summary>
        [MaxLength(1000)]
        public string Explanation { get; set; } = "";

        [JsonIgnore]
        public virtual Rule? Rule { get; set; }

        /// <summary>
        /// Порядок отображения упражнения внутри правила
        /// </summary>
        public int OrderIndex { get; set; }

        // Helper properties (not mapped to DB)
        [NotMapped]
        public string[] Options
        {
            get
            {
                try
                {
                    return System.Text.Json.JsonSerializer.Deserialize<string[]>(OptionsJson) ?? Array.Empty<string>();
                }
                catch
                {
                    return Array.Empty<string>();
                }
            }
            set
            {
                OptionsJson = System.Text.Json.JsonSerializer.Serialize(value ?? Array.Empty<string>());
            }
        }
    }
}
