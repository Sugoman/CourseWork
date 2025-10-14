using System.ComponentModel.DataAnnotations;

namespace EnglishLearningTrainer.Models
{
    public class User
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int RoleId { get; set; }

        [Required]
        [MaxLength(50)]
        public string Login { get; set; }

        [Required]
        [MaxLength(100)]
        public string PasswordHash { get; set; }

        public Role Role { get; set; } // "Admin", "Teacher", "Student"

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
