using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LearningTrainerShared.Models
{
    public class DashboardStats
    {
        public int TotalWords { get; set; }
        public int TotalDictionaries { get; set; }
        public int LearnedWords { get; set; }
        public double AverageSuccessRate { get; set; }
    }
}
