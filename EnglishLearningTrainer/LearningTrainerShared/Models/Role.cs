using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace LearningTrainerShared.Models
{
    public class Role
    {
        public int Id { get; set; }
        public string Name { get; set; } // "Admin", "Teacher", "Student"
        [JsonIgnore]
        public List<User> Users { get; set; }
    }
}
