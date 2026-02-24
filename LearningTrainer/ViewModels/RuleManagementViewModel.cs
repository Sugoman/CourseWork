using LearningTrainer.Core;
using LearningTrainer.Services;
using LearningTrainerShared.Models;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using static LearningTrainer.Core.EventAggregator;

namespace LearningTrainer.ViewModels
{
    public class ExerciseViewModel : ObservableObject
    {
        private string _question = "";
        public string Question
        {
            get => _question;
            set => SetProperty(ref _question, value);
        }

        private string _option1 = "";
        public string Option1
        {
            get => _option1;
            set => SetProperty(ref _option1, value);
        }

        private string _option2 = "";
        public string Option2
        {
            get => _option2;
            set => SetProperty(ref _option2, value);
        }

        private string _option3 = "";
        public string Option3
        {
            get => _option3;
            set => SetProperty(ref _option3, value);
        }

        private string _option4 = "";
        public string Option4
        {
            get => _option4;
            set => SetProperty(ref _option4, value);
        }

        private int _correctIndex;
        public int CorrectIndex
        {
            get => _correctIndex;
            set => SetProperty(ref _correctIndex, value);
        }

        private string _explanation = "";
        public string Explanation
        {
            get => _explanation;
            set => SetProperty(ref _explanation, value);
        }

        private int? _selectedAnswer;
        public int? SelectedAnswer
        {
            get => _selectedAnswer;
            set
            {
                if (SetProperty(ref _selectedAnswer, value))
                {
                    IsAnswered = value.HasValue;
                    if (value.HasValue)
                        IsCorrect = value.Value == CorrectIndex;
                }
            }
        }

        private bool _isAnswered;
        public bool IsAnswered
        {
            get => _isAnswered;
            set => SetProperty(ref _isAnswered, value);
        }

        private bool _isCorrect;
        public bool IsCorrect
        {
            get => _isCorrect;
            set => SetProperty(ref _isCorrect, value);
        }

        public int OrderIndex { get; set; }

        public string[] GetOptions()
        {
            return new[] { Option1, Option2, Option3, Option4 }
                .Where(o => !string.IsNullOrWhiteSpace(o))
                .ToArray();
        }

        public GrammarExercise ToModel()
        {
            return new GrammarExercise
            {
                Question = Question,
                Options = GetOptions(),
                CorrectIndex = CorrectIndex,
                Explanation = Explanation,
                OrderIndex = OrderIndex
            };
        }

        public static ExerciseViewModel FromModel(GrammarExercise exercise)
        {
            var options = exercise.Options;
            return new ExerciseViewModel
            {
                Question = exercise.Question,
                Option1 = options.Length > 0 ? options[0] : "",
                Option2 = options.Length > 1 ? options[1] : "",
                Option3 = options.Length > 2 ? options[2] : "",
                Option4 = options.Length > 3 ? options[3] : "",
                CorrectIndex = exercise.CorrectIndex,
                Explanation = exercise.Explanation,
                OrderIndex = exercise.OrderIndex
            };
        }

        public void Reset()
        {
            SelectedAnswer = null;
            IsAnswered = false;
            IsCorrect = false;
        }
    }

    public class RuleManagementViewModel : TabViewModelBase
    {
        private readonly IDataService _dataService;
        private readonly SettingsService _settingsService; 
        private readonly Rule _ruleModel;

        private string _ruleTitle = "";
        [Required(ErrorMessage = "Заголовок обязателен")]
        [MaxLength(70, ErrorMessage = "Максимум 70 символов")]
        public string RuleTitle
        {
            get => _ruleTitle;
            set => SetProperty(ref _ruleTitle, value);
        }

        private string _description = "";
        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        private string _category = "";
        public string Category
        {
            get => _category;
            set => SetProperty(ref _category, value);
        }

        private int _difficultyLevel;
        public int DifficultyLevel
        {
            get => _difficultyLevel;
            set => SetProperty(ref _difficultyLevel, value);
        }

        public bool IsEditable { get; }

        public ICommand SaveChangesCommand { get; }
        public ICommand CloseCommand { get; }
        public ICommand PublishToMarketplaceCommand { get; }
        public ICommand AddExerciseCommand { get; }
        public ICommand RemoveExerciseCommand { get; }
        public ICommand CheckAnswerCommand { get; }
        public ICommand ResetExercisesCommand { get; }

        public ObservableCollection<ExerciseViewModel> Exercises { get; } = new();

        public bool HasExercises => Exercises.Count > 0;

        private bool _isPublished;
        public bool IsPublished
        {
            get => _isPublished;
            set => SetProperty(ref _isPublished, value);
        }

        private int _correctAnswersCount;
        public int CorrectAnswersCount
        {
            get => _correctAnswersCount;
            set => SetProperty(ref _correctAnswersCount, value);
        }

        private int _answeredCount;
        public int AnsweredCount
        {
            get => _answeredCount;
            set => SetProperty(ref _answeredCount, value);
        }

        public bool AllAnswered => Exercises.Count > 0 && AnsweredCount == Exercises.Count;

        public string PublishButtonText => IsPublished ? "Снять с публикации" : "Опубликовать на сайте";

        public RuleManagementViewModel(IDataService dataService, SettingsService settingsService, Rule rule, int currentUserId)
        {
            _dataService = dataService;
            _settingsService = settingsService;
            _ruleModel = rule;

            IsEditable = rule.UserId == currentUserId;
            IsPublished = rule.IsPublished;

            RuleTitle = rule.Title;
            Description = rule.Description;
            Category = rule.Category;
            DifficultyLevel = rule.DifficultyLevel;

            MarkdownContent = rule.MarkdownContent;

            // Load exercises
            if (rule.Exercises != null)
            {
                foreach (var exercise in rule.Exercises.OrderBy(e => e.OrderIndex))
                {
                    Exercises.Add(ExerciseViewModel.FromModel(exercise));
                }
            }
            OnPropertyChanged(nameof(HasExercises));

            Config = _settingsService.CurrentMarkdownConfig;
            _settingsService.MarkdownConfigChanged += OnConfigChanged;

            var tabTitleKey = IsEditable ? "Loc.Tab.EditRule" : "Loc.Tab.ViewRule";
            SetLocalizedTitle(tabTitleKey, $": {rule.Title}");

            SaveChangesCommand = new RelayCommand(
                async (_) => await SaveChanges(),
                (_) => IsEditable && !HasErrors && !string.IsNullOrWhiteSpace(RuleTitle));
            CloseCommand = new RelayCommand((_) => CloseTab());
            PublishToMarketplaceCommand = new RelayCommand(async (_) => await TogglePublish(), (_) => IsEditable);
            AddExerciseCommand = new RelayCommand((_) => AddExercise(), (_) => IsEditable);
            RemoveExerciseCommand = new RelayCommand((param) => RemoveExercise(param as ExerciseViewModel), (_) => IsEditable);
            CheckAnswerCommand = new RelayCommand((param) => CheckAnswer(param));
            ResetExercisesCommand = new RelayCommand((_) => ResetExercises(), (_) => AnsweredCount > 0);

            PropertyChanged += (_, e) =>
            {
                if (e.PropertyName is nameof(RuleTitle) or nameof(HasErrors))
                    (SaveChangesCommand as RelayCommand)?.RaiseCanExecuteChanged();
            };
        }
        
        private async Task TogglePublish()
        {
            bool success;
            string action;
            
            if (IsPublished)
            {
                success = await _dataService.UnpublishRuleAsync(_ruleModel.Id);
                action = "снято с публикации";
            }
            else
            {
                success = await _dataService.PublishRuleAsync(_ruleModel.Id);
                action = "опубликовано на сайте";
            }
            
            if (success)
            {
                IsPublished = !IsPublished;
                _ruleModel.IsPublished = IsPublished;
                OnPropertyChanged(nameof(PublishButtonText));
                
                EventAggregator.Instance.Publish(ShowNotificationMessage.Success(
                    "Успешно",
                    $"Правило '{RuleTitle}' {action}!"));
            }
            else
            {
                EventAggregator.Instance.Publish(ShowNotificationMessage.Error(
                    "Ошибка",
                    "Не удалось изменить статус публикации"));
            }
        }

        private string _markdownContent = "";
        [Required(ErrorMessage = "Содержание обязательно")]
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
            _ruleModel.Title = RuleTitle;
            _ruleModel.Description = Description;
            _ruleModel.MarkdownContent = MarkdownContent;
            _ruleModel.Category = Category;
            _ruleModel.DifficultyLevel = DifficultyLevel;

            // Sync exercises to model
            _ruleModel.Exercises = Exercises.Select((e, idx) =>
            {
                var model = e.ToModel();
                model.RuleId = _ruleModel.Id;
                model.OrderIndex = idx;
                return model;
            }).ToList();

            var success = await _dataService.UpdateRuleAsync(_ruleModel);

            if (success)
            {
                EventAggregator.Instance.Publish(ShowNotificationMessage.Success(
                    "Сохранено",
                    "Правило успешно сохранено!"));
                TitleSuffix = $": {RuleTitle}";
                UpdateLocalizedTitle();
            }
            else
            {
                EventAggregator.Instance.Publish(ShowNotificationMessage.Error(
                    "Ошибка",
                    "Ошибка при сохранении на сервере"));
            }
        }

        private void AddExercise()
        {
            var exercise = new ExerciseViewModel
            {
                OrderIndex = Exercises.Count
            };
            Exercises.Add(exercise);
            OnPropertyChanged(nameof(HasExercises));
        }

        private void RemoveExercise(ExerciseViewModel? exercise)
        {
            if (exercise != null)
            {
                Exercises.Remove(exercise);
                // Re-index
                for (int i = 0; i < Exercises.Count; i++)
                    Exercises[i].OrderIndex = i;
                OnPropertyChanged(nameof(HasExercises));
                UpdateExerciseStats();
            }
        }

        private void CheckAnswer(object? param)
        {
            if (param is object[] parts && parts.Length == 2
                && parts[0] is ExerciseViewModel exercise
                && parts[1] is int answerIndex)
            {
                if (!exercise.IsAnswered)
                {
                    exercise.SelectedAnswer = answerIndex;
                    UpdateExerciseStats();
                }
            }
        }

        private void ResetExercises()
        {
            foreach (var ex in Exercises)
                ex.Reset();
            UpdateExerciseStats();
        }

        private void UpdateExerciseStats()
        {
            AnsweredCount = Exercises.Count(e => e.IsAnswered);
            CorrectAnswersCount = Exercises.Count(e => e.IsAnswered && e.IsCorrect);
            OnPropertyChanged(nameof(AllAnswered));
            (ResetExercisesCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        private void CloseTab()
        {
            _settingsService.MarkdownConfigChanged -= OnConfigChanged;
            EventAggregator.Instance.Publish(new CloseTabMessage(this));
        }
    }
}
