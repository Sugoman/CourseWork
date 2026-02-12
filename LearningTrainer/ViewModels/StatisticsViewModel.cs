using LearningTrainer.Core;
using LearningTrainer.Services;
using LearningTrainerShared.Models.Statistics;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace LearningTrainer.ViewModels
{
    public class StatisticsViewModel : TabViewModelBase
    {
        private readonly IDataService _dataService;
        private bool _isLoading;
        private UserStatistics? _statistics;
        private string _selectedPeriod = "week";

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public UserStatistics? Statistics
        {
            get => _statistics;
            set
            {
                if (SetProperty(ref _statistics, value))
                {
                    OnPropertyChanged(nameof(ProgressPercent));
                    OnPropertyChanged(nameof(StreakMessage));
                    OnPropertyChanged(nameof(HasData));
                    OnPropertyChanged(nameof(FormattedLearningTime));
                }
            }
        }

        public string SelectedPeriod
        {
            get => _selectedPeriod;
            set
            {
                if (SetProperty(ref _selectedPeriod, value))
                {
                    _ = LoadStatisticsAsync();
                }
            }
        }

        public bool HasData => Statistics != null && Statistics.TotalWords > 0;

        public double ProgressPercent => Statistics?.TotalWords > 0
            ? (double)Statistics.LearnedWords / Statistics.TotalWords * 100
            : 0;

        public string StreakMessage => Statistics?.CurrentStreak switch
        {
            0 or null => GetLocalized("Loc.Statistics.StartStreak"),
            1 => "1 " + GetLocalized("Loc.Statistics.DayStreak") + " 🔥",
            var n when n < 7 => $"{n} " + GetLocalized("Loc.Statistics.DaysStreak") + " 🔥",
            var n when n < 30 => $"{n} " + GetLocalized("Loc.Statistics.DaysStreak") + " 🔥🔥",
            var n => $"{n} " + GetLocalized("Loc.Statistics.DaysStreak") + " 🔥🔥🔥"
        };

        public string FormattedLearningTime
        {
            get
            {
                if (Statistics == null) return "0м";
                var time = Statistics.TotalLearningTime;
                if (time.TotalHours >= 1)
                    return $"{time.TotalHours:F1}ч";
                if (time.TotalMinutes >= 1)
                    return $"{time.TotalMinutes:F0}м";
                return "< 1м";
            }
        }

        public ObservableCollection<PeriodOption> PeriodOptions { get; } = new()
        {
            new PeriodOption("week", "Неделя"),
            new PeriodOption("month", "Месяц"),
            new PeriodOption("3months", "3 месяца"),
            new PeriodOption("year", "Год"),
            new PeriodOption("all", "Всё время")
        };

        public ICommand RefreshCommand { get; }
        public ICommand PracticeDifficultWordsCommand { get; }

        public StatisticsViewModel(IDataService dataService)
        {
            _dataService = dataService;
            SetLocalizedTitle("Loc.Tab.Statistics");

            RefreshCommand = new RelayCommand(async _ => await LoadStatisticsAsync());
            PracticeDifficultWordsCommand = new RelayCommand(PracticeDifficultWords);

            _ = LoadStatisticsAsync();
        }

        /// <summary>
        /// Активные достижения: недавно разблокированные или с прогрессом > 0
        /// </summary>
        public IEnumerable<LearningTrainerShared.Models.Statistics.Achievement> ActiveAchievements =>
            Statistics?.Achievements?.Where(a =>
                (a.IsUnlocked && a.UnlockedAt.HasValue && (DateTime.UtcNow - a.UnlockedAt.Value).TotalDays <= 7) ||
                (!a.IsUnlocked && a.Progress > 0))
            .OrderByDescending(a => a.IsUnlocked)
            .ThenByDescending(a => a.Progress)
            .Take(6) ?? Enumerable.Empty<LearningTrainerShared.Models.Statistics.Achievement>();

        /// <summary>
        /// Количество разблокированных достижений
        /// </summary>
        public int UnlockedAchievementsCount => Statistics?.Achievements?.Count(a => a.IsUnlocked) ?? 0;

        /// <summary>
        /// Общее количество достижений
        /// </summary>
        public int TotalAchievementsCount => Statistics?.Achievements?.Count ?? 0;

        private async Task LoadStatisticsAsync()
        {
            IsLoading = true;
            try
            {
                Statistics = await _dataService.GetStatisticsAsync(_selectedPeriod);
                OnPropertyChanged(nameof(ActiveAchievements));
                OnPropertyChanged(nameof(UnlockedAchievementsCount));
                OnPropertyChanged(nameof(TotalAchievementsCount));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading statistics: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void PracticeDifficultWords(object? parameter)
        {
            if (Statistics?.DifficultWords?.Any() == true)
            {
                // TODO: Open training with difficult words
                MessageBox.Show(
                    GetLocalized("Loc.Statistics.DifficultWordsTraining"),
                    GetLocalized("Loc.Statistics.Title"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
    }

    public class PeriodOption
    {
        public string Value { get; }
        public string DisplayName { get; }

        public PeriodOption(string value, string displayName)
        {
            Value = value;
            DisplayName = displayName;
        }
    }
}
