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
        public string Login { get; set; } = "";

        [MaxLength(100)]
        public string? Email { get; set; }

        [Required]
        [MaxLength(100)]
        public string PasswordHash { get; set; } = "";

        public Role Role { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Refresh Token поля
        public string? RefreshToken { get; set; }
        public DateTime? RefreshTokenExpiryTime { get; set; }
        public bool IsRefreshTokenRevoked { get; set; } = false;

        public int? UserId { get; set; }
        [JsonIgnore]
        [ForeignKey("UserId")] 
        public User? Teacher { get; set; }
        [JsonIgnore]
        public virtual ICollection<User> Students { get; set; } = new List<User>();
        public string? InviteCode { get; set; }
    }
}
