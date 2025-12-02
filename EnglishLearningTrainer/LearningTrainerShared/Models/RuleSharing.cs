using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LearningTrainerShared.Models
{
    public class RuleSharing
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int RuleId { get; set; }
        [ForeignKey("RuleId")]
        public Rule Rule { get; set; } = null!;

        [Required]
        public int StudentId { get; set; }
        [ForeignKey("StudentId")]
        public User User { get; set; } = null!;

        public DateTime SharedAt { get; set; } = DateTime.UtcNow;
    }
}
