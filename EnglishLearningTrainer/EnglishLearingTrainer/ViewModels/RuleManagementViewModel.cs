using LearningTrainer.Core;
using LearningTrainer.Services;
using LearningTrainerShared.Models;
using System.Windows;
using System.Windows.Input;
using static LearningTrainer.Core.EventAggregator;

namespace LearningTrainer.ViewModels
{
    public class RuleManagementViewModel : TabViewModelBase
    {
        private readonly IDataService _dataService;
        private readonly Rule _ruleModel; // Ссылка на оригинал

        // Поля для привязки к TextBox
        public string Title { get; set; }
        public string Description { get; set; }
        public string MarkdownContent { get; set; }
        public string Category { get; set; }
        public int DifficultyLevel { get; set; }

        public bool IsEditable { get; }

        public ICommand SaveChangesCommand { get; }
        public ICommand CloseCommand { get; }

        public RuleManagementViewModel(IDataService dataService, Rule rule, int currentUserId)
        {
            _dataService = dataService;
            _ruleModel = rule;

            IsEditable = rule.UserId == currentUserId;

            Title = rule.Title;
            Description = rule.Description;
            MarkdownContent = rule.MarkdownContent;
            Category = rule.Category;
            DifficultyLevel = rule.DifficultyLevel;

            base.Title = $"Edit Rule: {rule.Title}";

            SaveChangesCommand = new RelayCommand(async (_) => await SaveChanges(), (_) => IsEditable);
            CloseCommand = new RelayCommand((_) => EventAggregator.Instance.Publish(new CloseTabMessage(this)));
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
                MessageBox.Show("Правило успешно сохранено!", "Успех");
                base.Title = $"Edit Rule: {Title}";
            }
            else
            {
                MessageBox.Show("Ошибка при сохранении на сервере.", "Ошибка");
            }
        }
    }
}