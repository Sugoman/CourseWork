using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace LearningTrainerShared.Models
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

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public int? UserId { get; set; }
        [JsonIgnore]
        [ForeignKey("UserId")] 
        public User? Teacher { get; set; }
        [JsonIgnore]
        public virtual ICollection<User> Students { get; set; }
        public string? InviteCode { get; set; }
    }
}
