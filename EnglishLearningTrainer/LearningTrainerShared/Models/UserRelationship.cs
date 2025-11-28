using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LearningTrainerShared.Models
{
    public class UserRelationship
    {
        public int Id { get; set; }

        public int TeacherId { get; set; }

        public int StudentId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual User Teacher { get; set; }
        public virtual User Student { get; set; }
    }
}
