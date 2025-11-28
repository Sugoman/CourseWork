using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LearningTrainerShared.Models
{
    public class DictionarySharing
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int DictionaryId { get; set; }
        [ForeignKey("DictionaryId")]
        public Dictionary Dictionary { get; set; } = null!;

        [Required]
        public int StudentId { get; set; }
        [ForeignKey("StudentId")]
        public User User { get; set; } = null!;

        public DateTime SharedAt { get; set; } = DateTime.UtcNow;
    }
}
