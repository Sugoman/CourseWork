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

        public List<ActivityPoint> ActivityLast7Days { get; set; } = new();
        public List<KnowledgeDistributionPoint> KnowledgeDistribution { get; set; } = new();
    }

    public class ActivityPoint
    {
        public DateTime Date { get; set; }
        public int Reviewed { get; set; }
        public int Learned { get; set; }
    }

    public class KnowledgeDistributionPoint
    {
        public int Level { get; set; }
        public int Count { get; set; }
    }
}
