using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LearningTrainerShared.Models
{
    public class StudentDto
    {
        public int Id { get; set; }
        public string Login { get; set; } = string.Empty;

        public int? WordsLearned { get; set; }
    }
}
