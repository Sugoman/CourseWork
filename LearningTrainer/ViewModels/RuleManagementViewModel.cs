using LearningTrainer.Core;
using LearningTrainer.Services;
using LearningTrainerShared.Models;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using static LearningTrainer.Core.EventAggregator;

namespace LearningTrainer.ViewModels
{
    public class RuleManagementViewModel : TabViewModelBase
    {
        private readonly IDataService _dataService;
        private readonly SettingsService _settingsService; 
        private readonly Rule _ruleModel;

        public string Title { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public int DifficultyLevel { get; set; }

        public bool IsEditable { get; }

        public ICommand SaveChangesCommand { get; }
        public ICommand CloseCommand { get; }

        public RuleManagementViewModel(IDataService dataService, SettingsService settingsService, Rule rule, int currentUserId)
        {
            _dataService = dataService;
            _settingsService = settingsService;
            _ruleModel = rule;

            IsEditable = rule.UserId == currentUserId;

            Title = rule.Title;
            Description = rule.Description;
            Category = rule.Category;
            DifficultyLevel = rule.DifficultyLevel;

            MarkdownContent = rule.MarkdownContent;

            Config = _settingsService.CurrentMarkdownConfig;
            _settingsService.MarkdownConfigChanged += OnConfigChanged;

            var tabTitleKey = IsEditable ? "Loc.Tab.EditRule" : "Loc.Tab.ViewRule";
            SetLocalizedTitle(tabTitleKey, $": {rule.Title}");

            SaveChangesCommand = new RelayCommand(async (_) => await SaveChanges(), (_) => IsEditable);
            CloseCommand = new RelayCommand((_) => CloseTab());
        }

        private string _markdownContent;
        public string MarkdownContent
        {
            get => _markdownContent;
            set => SetProperty(ref _markdownContent, value);
        }

        private MarkdownConfig _config;
        public MarkdownConfig Config
        {
            get => _config;
            set => SetProperty(ref _config, value);
        }

        // Обработчик смены темы
        private void OnConfigChanged(MarkdownConfig newConfig)
        {
            Config = newConfig;
        }

        private async Task SaveChanges()
        {
            _ruleModel.Title = Title;
            _ruleModel.Description = Description;
            _ruleModel.MarkdownContent = MarkdownContent;
            _ruleModel.Category = Category;
            _ruleModel.DifficultyLevel = DifficultyLevel;

            var success = await _dataService.UpdateRuleAsync(_ruleModel);

            if (success)
            {
                EventAggregator.Instance.Publish(ShowNotificationMessage.Success(
                    "Сохранено",
                    "Правило успешно сохранено!"));
                TitleSuffix = $": {Title}";
                UpdateLocalizedTitle();
            }
            else
            {
                EventAggregator.Instance.Publish(ShowNotificationMessage.Error(
                    "Ошибка",
                    "Ошибка при сохранении на сервере"));
            }
        }

        private void CloseTab()
        {
            _settingsService.MarkdownConfigChanged -= OnConfigChanged;
            EventAggregator.Instance.Publish(new CloseTabMessage(this));
        }
    }
}