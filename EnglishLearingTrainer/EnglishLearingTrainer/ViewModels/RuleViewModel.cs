using EnglishLearningTrainer.Core;
using EnglishLearningTrainer.Models;

namespace EnglishLearningTrainer.ViewModels
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