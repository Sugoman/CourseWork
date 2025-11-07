using LearningTrainer.Core;
using LearningTrainer.Services;
using LearningTrainerShared.Models;
using System.Windows.Input;

namespace LearningTrainer.ViewModels
{
    public class AddRuleViewModel : TabViewModelBase
    {
        private readonly IDataService _dataService;

        public string RuleTitle { get; set; }
        public string Description { get; set; }
        public string MarkdownContent { get; set; }
        public string Category { get; set; } = "Grammar";
        public int DifficultyLevel { get; set; } = 1;

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }

        public AddRuleViewModel(IDataService dataService)
        {
            _dataService = dataService;
            Title = "Создать новое правило";

            SaveCommand = new RelayCommand(async (param) => await SaveRuleAsync());
            CancelCommand = new RelayCommand((param) => Cancel());
        }

        private async Task SaveRuleAsync()
        {
            if (string.IsNullOrWhiteSpace(RuleTitle) || string.IsNullOrWhiteSpace(MarkdownContent))
            {
                System.Diagnostics.Debug.WriteLine("Ошибка: не заполнены обязательные поля");
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
                    CreatedAt = DateTime.Now
                };

                var savedRule = await _dataService.AddRuleAsync(newRule);
                System.Diagnostics.Debug.WriteLine($"Правило '{RuleTitle}' успешно создано! ID: {savedRule.Id}");

                EventAggregator.Instance.Publish(new RuleAddedMessage(savedRule));

                EventAggregator.Instance.Publish(new EventAggregator.CloseTabMessage(this));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка при создании правила: {ex.Message}");
            }
        }

        private void Cancel()
        {
            System.Diagnostics.Debug.WriteLine("Создание правила отменено");
            EventAggregator.Instance.Publish(this);
        }
    }
}