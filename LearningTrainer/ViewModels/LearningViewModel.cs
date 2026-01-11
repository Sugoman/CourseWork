using LearningTrainer.Core;
using LearningTrainer.Services;
using LearningTrainerShared.Models;
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

            LoadSessionAsync();
        }

        private async void LoadSessionAsync()
        {
            try
            {
                var sessionWords = await _dataService.GetReviewSessionAsync(_dictionaryId);

                if (sessionWords == null || !sessionWords.Any())
                {
                    IsSessionComplete = true; 
                    return;
                }

                _wordsQueue = new Queue<Word>(sessionWords);
                CurrentWord = _wordsQueue.Peek();
                IsFlipped = false;

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
                IsSessionComplete = true; 
                return;
            }

            CurrentWord = null;

            IsFlipped = false;

            await Task.Delay(450);

            CurrentWord = _wordsQueue.Peek();
        }

        private void CloseTab(object parameter)
        {
            EventAggregator.Instance.Publish(new CloseTabMessage(this));
        }
    }
}