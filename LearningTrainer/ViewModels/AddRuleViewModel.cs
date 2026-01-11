using LearningTrainer.Core;
using LearningTrainer.Services;
using LearningTrainerShared.Models;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using static LearningTrainer.Core.EventAggregator;

namespace LearningTrainer.ViewModels
{
    public class AddRuleViewModel : TabViewModelBase
    {
        private readonly IDataService _dataService;
        private readonly SettingsService _settingsService;

        private string _ruleTitle;
        private string _description;
        private string _markdownContent;
        private string _category = "Grammar";
        private int _difficultyLevel = 1;
        private MarkdownConfig _config;

        public string RuleTitle
        {
            get => _ruleTitle;
            set => SetProperty(ref _ruleTitle, value);
        }

        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        public string MarkdownContent
        {
            get => _markdownContent;
            set => SetProperty(ref _markdownContent, value);
        }

        public string Category
        {
            get => _category;
            set => SetProperty(ref _category, value);
        }

        public int DifficultyLevel
        {
            get => _difficultyLevel;
            set => SetProperty(ref _difficultyLevel, value);
        }

        public MarkdownConfig Config
        {
            get => _config;
            set => SetProperty(ref _config, value);
        }

        public int UserId { get; set; }

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }

        public AddRuleViewModel(IDataService dataService, SettingsService settingsService)
        {
            _dataService = dataService;
            _settingsService = settingsService;
            SetLocalizedTitle("Loc.Tab.CreateRule");

            // Загружаем цвета темы
            Config = _settingsService.CurrentMarkdownConfig;
            _settingsService.MarkdownConfigChanged += OnConfigChanged;

            SaveCommand = new RelayCommand(async (param) => await SaveRuleAsync());
            CancelCommand = new RelayCommand((param) => Cancel());
        }

        private void OnConfigChanged(MarkdownConfig newConfig)
        {
            Config = newConfig;
        }

        private async Task SaveRuleAsync()
        {
            if (string.IsNullOrWhiteSpace(RuleTitle) || string.IsNullOrWhiteSpace(MarkdownContent))
            {
                EventAggregator.Instance.Publish(ShowNotificationMessage.Error(
                    "Ошибка валидации",
                    "Заполните заголовок и содержание!"));
                return;
            }

            try
            {
                var newRule = new Rule
                {
                    Title = RuleTitle.Trim(),
                    Description = Description?.Trim() ?? "",
                    MarkdownContent = MarkdownContent.Trim(),
                    Category = Category.Trim(),
                    DifficultyLevel = DifficultyLevel,
                    CreatedAt = DateTime.UtcNow
                };

                var savedRule = await _dataService.AddRuleAsync(newRule);

                EventAggregator.Instance.Publish(new RuleAddedMessage(savedRule));
                EventAggregator.Instance.Publish(new CloseTabMessage(this));
            }
            catch (Exception ex)
            {
                EventAggregator.Instance.Publish(ShowNotificationMessage.Error(
                    "Ошибка создания",
                    ex.Message));
            }
        }

        private void Cancel()
        {
            EventAggregator.Instance.Publish(new CloseTabMessage(this));
        }
    }
}