using LearningTrainer.Core;
using LearningTrainer.Services;
using LearningTrainerShared.Models;
using System.Collections.ObjectModel;
using System.Windows.Input;

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

        public ObservableCollection<ExerciseViewModel> Exercises { get; } = new();
        public bool HasExercises => Exercises.Count > 0;

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

        public ICommand CheckAnswerCommand { get; }
        public ICommand ResetExercisesCommand { get; }

        public RuleViewModel(Rule rule, SettingsService settingsService)
        {
            Rule = rule;
            _settingsService = settingsService;

            Title = $"Rule: {rule.Title}";

            Config = _settingsService.CurrentMarkdownConfig;

            if (rule.Exercises != null)
            {
                foreach (var exercise in rule.Exercises.OrderBy(e => e.OrderIndex))
                {
                    Exercises.Add(ExerciseViewModel.FromModel(exercise));
                }
            }
            OnPropertyChanged(nameof(HasExercises));

            CheckAnswerCommand = new RelayCommand((param) => CheckAnswer(param));
            ResetExercisesCommand = new RelayCommand((_) => ResetExercises(), (_) => AnsweredCount > 0);

            _settingsService.MarkdownConfigChanged += OnConfigChanged;
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

        private void OnConfigChanged(MarkdownConfig newConfig)
        {
            Config = newConfig;
        }
    }
}
