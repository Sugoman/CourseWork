using LearningTrainer.Core;
using LearningTrainerShared.Models;
using Microsoft.VisualBasic.ApplicationServices;

namespace LearningTrainer.ViewModels
{
    public class RuleViewModel : TabViewModelBase
    {
        public Rule Rule { get; }
        public int UserId { get; }

        public RuleViewModel(Rule rule)
        {
            Rule = rule;
            Title = $"Правило: {rule.Title}";
            UserId = -1;
        }
    }
}