using LearningTrainer.Core;
using LearningTrainerShared.Models;

namespace LearningTrainer.ViewModels
{
    public class RuleViewModel : TabViewModelBase
    {
        public Rule Rule { get; }

        public RuleViewModel(Rule rule)
        {
            Rule = rule;
            Title = $"Правило: {rule.Title}";
        }
    }
}