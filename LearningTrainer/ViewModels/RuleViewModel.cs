using LearningTrainer.Core;
using LearningTrainer.Services;
using LearningTrainerShared.Models;

namespace LearningTrainer.ViewModels
{
    public class RuleViewModel : TabViewModelBase
    {
        private readonly SettingsService _settingsService;

        public Rule Rule { get; }

        private MarkdownConfig _config;
        public MarkdownConfig Config
        {
            get => _config;
            set => SetProperty(ref _config, value);
        }

        public RuleViewModel(Rule rule, SettingsService settingsService)
        {
            Rule = rule;
            _settingsService = settingsService;

            Title = $"Rule: {rule.Title}";

           Config = _settingsService.CurrentMarkdownConfig;

            _settingsService.MarkdownConfigChanged += OnConfigChanged;
        }

        private void OnConfigChanged(MarkdownConfig newConfig)
        {
            Config = newConfig;
        }
    }
}
