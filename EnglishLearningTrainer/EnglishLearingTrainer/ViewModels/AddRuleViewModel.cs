using LearningTrainer.Core;
using LearningTrainer.Services;
using LearningTrainerShared.Models;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
// Добавь, чтобы видеть CloseTabMessage
using static LearningTrainer.Core.EventAggregator;

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
        public int UserId { get; set; }

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
                MessageBox.Show("Заполните заголовок и содержание!", "Ошибка");
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
                MessageBox.Show($"Ошибка создания: {ex.Message}");
            }
        }

        private void Cancel()
        {
            EventAggregator.Instance.Publish(new CloseTabMessage(this));
        }
    }
}