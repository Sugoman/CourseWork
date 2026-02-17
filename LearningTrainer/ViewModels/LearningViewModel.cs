using LearningTrainer.Core;
using LearningTrainer.Services;
using LearningTrainerShared.Models;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using static LearningTrainer.Core.EventAggregator;

namespace LearningTrainer.ViewModels
{
    public class LearningViewModel : TabViewModelBase
    {
        private readonly IDataService _dataService;
        private readonly int _dictionaryId;
        private Queue<Word> _wordsQueue;
        private readonly Stopwatch _sessionStopwatch = new();
        private int _totalWordsCount;

        private Word _currentWord;
        public Word CurrentWord
        {
            get => _currentWord;
            set => SetProperty(ref _currentWord, value);
        }


        private bool _isFlipped;
        public bool IsFlipped
        {
            get => _isFlipped;
            set
            {
                if (SetProperty(ref _isFlipped, value))
                {
                    (AnswerCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        private bool _isSessionComplete;
        public bool IsSessionComplete
        {
            get => _isSessionComplete;
            set => SetProperty(ref _isSessionComplete, value);
        }

        // --- Session result properties ---

        private int _correctCount;
        public int CorrectCount
        {
            get => _correctCount;
            set => SetProperty(ref _correctCount, value);
        }

        private int _wrongCount;
        public int WrongCount
        {
            get => _wrongCount;
            set => SetProperty(ref _wrongCount, value);
        }

        public int TotalWordsCount => _totalWordsCount;

        private string _sessionDuration;
        public string SessionDuration
        {
            get => _sessionDuration;
            set => SetProperty(ref _sessionDuration, value);
        }

        private double _accuracyPercent;
        public double AccuracyPercent
        {
            get => _accuracyPercent;
            set => SetProperty(ref _accuracyPercent, value);
        }

        private string _resultEmoji;
        public string ResultEmoji
        {
            get => _resultEmoji;
            set => SetProperty(ref _resultEmoji, value);
        }

        private string _resultMessage;
        public string ResultMessage
        {
            get => _resultMessage;
            set => SetProperty(ref _resultMessage, value);
        }

        public ObservableCollection<WordResultDto> DifficultWords { get; } = new();

        public ICommand FlipCardCommand { get; }

        public ICommand AnswerCommand { get; }

        public ICommand CloseTabCommand { get; }

        public LearningViewModel(IDataService dataService, int dictionaryId, string dictionaryName)
        {
            _dataService = dataService;
            _dictionaryId = dictionaryId;
            SetLocalizedTitle("Loc.Tab.Learning", $": {dictionaryName}"); 

            FlipCardCommand = new RelayCommand(
                (param) => IsFlipped = true,
                (param) => !IsFlipped && CurrentWord != null
            );

            AnswerCommand = new RelayCommand(
               async (param) =>
               {
                   if (param is ResponseQuality quality)
                       await HandleAnswerAsync(quality);
               },
               (param) => IsFlipped && CurrentWord != null 
           );
            CloseTabCommand = new RelayCommand(CloseTab);

            _ = LoadSessionAsync();
        }

        private async Task LoadSessionAsync()
        {
            try
            {
                var sessionWords = await _dataService.GetReviewSessionAsync(_dictionaryId);

                if (sessionWords == null || !sessionWords.Any())
                {
                    IsSessionComplete = true; 
                    return;
                }

                _totalWordsCount = sessionWords.Count;
                OnPropertyChanged(nameof(TotalWordsCount));
                _wordsQueue = new Queue<Word>(sessionWords);
                CurrentWord = _wordsQueue.Peek();
                IsFlipped = false;
                _sessionStopwatch.Start();

                (FlipCardCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (AnswerCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
            catch (System.Net.Http.HttpRequestException ex)
            {
                // 401, 500
                EventAggregator.Instance.Publish(ShowNotificationMessage.Error(
                    "Ошибка загрузки",
                    $"Ошибка загрузки сессии: {ex.Message}"));
                CloseTab(null);
            }
        }

        private async Task HandleAnswerAsync(ResponseQuality quality)
        {
            var request = new UpdateProgressRequest
            {
                WordId = CurrentWord.Id,
                Quality = quality
            };

            // Track result for this word
            if (quality >= ResponseQuality.Good)
                CorrectCount++;
            else
            {
                WrongCount++;
                DifficultWords.Add(new WordResultDto
                {
                    WordId = CurrentWord.Id,
                    OriginalWord = CurrentWord.OriginalWord,
                    Quality = quality
                });
            }

            try
            {
                // POST /api/progress/update
                await _dataService.UpdateProgressAsync(request);
                EventAggregator.Instance.Publish(new RefreshDataMessage());
            }
            catch (System.Net.Http.HttpRequestException ex)
            {
                EventAggregator.Instance.Publish(ShowNotificationMessage.Error(
                    "Ошибка",
                    $"Ошибка сохранения прогресса: {ex.Message}"));
            }

            var wordToRequeue = _wordsQueue.Dequeue(); 

            if (quality == ResponseQuality.Again || quality == ResponseQuality.Hard)
            {
                _wordsQueue.Enqueue(wordToRequeue);
            }

            if (!_wordsQueue.Any())
            {
                CompleteSession();
                return;
            }

            CurrentWord = null;

            IsFlipped = false;

            await Task.Delay(450);

            CurrentWord = _wordsQueue.Peek();
        }

        private void CompleteSession()
        {
            _sessionStopwatch.Stop();
            var elapsed = _sessionStopwatch.Elapsed;

            if (elapsed.TotalHours >= 1)
                SessionDuration = $"{(int)elapsed.TotalHours}ч {elapsed.Minutes}мин";
            else if (elapsed.TotalMinutes >= 1)
                SessionDuration = $"{elapsed.Minutes}мин {elapsed.Seconds}с";
            else
                SessionDuration = $"{elapsed.Seconds}с";

            AccuracyPercent = _totalWordsCount > 0
                ? Math.Round((double)CorrectCount / _totalWordsCount * 100, 0)
                : 0;

            if (AccuracyPercent >= 90)
            {
                ResultEmoji = "🏆";
                ResultMessage = "Превосходно!";
            }
            else if (AccuracyPercent >= 70)
            {
                ResultEmoji = "🎉";
                ResultMessage = "Отличная работа!";
            }
            else if (AccuracyPercent >= 50)
            {
                ResultEmoji = "👍";
                ResultMessage = "Хороший результат!";
            }
            else
            {
                ResultEmoji = "💪";
                ResultMessage = "Продолжайте тренироваться!";
            }

            IsSessionComplete = true;
        }

        private void CloseTab(object parameter)
        {
            EventAggregator.Instance.Publish(new CloseTabMessage(this));
        }
    }
}
