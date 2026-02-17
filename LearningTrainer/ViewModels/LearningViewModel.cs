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
        private List<Word> _allWords = new();
        private readonly Stopwatch _sessionStopwatch = new();
        private int _totalWordsCount;
        private readonly Random _random = new();

        private Word _currentWord;
        public Word CurrentWord
        {
            get => _currentWord;
            set => SetProperty(ref _currentWord, value);
        }

        // --- Exercise type ---
        private string _exerciseType = "flashcard";
        public string ExerciseType
        {
            get => _exerciseType;
            set
            {
                if (SetProperty(ref _exerciseType, value))
                {
                    OnPropertyChanged(nameof(IsFlashcardMode));
                    OnPropertyChanged(nameof(IsMcqMode));
                    OnPropertyChanged(nameof(IsTypingMode));
                    PrepareCurrentExercise();
                }
            }
        }

        public bool IsFlashcardMode => ExerciseType == "flashcard";
        public bool IsMcqMode => ExerciseType == "mcq";
        public bool IsTypingMode => ExerciseType == "typing";

        private bool _isExerciseTypeChosen;
        public bool IsExerciseTypeChosen
        {
            get => _isExerciseTypeChosen;
            set => SetProperty(ref _isExerciseTypeChosen, value);
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

        // --- MCQ state ---
        private ObservableCollection<string> _mcqOptions = new();
        public ObservableCollection<string> McqOptions
        {
            get => _mcqOptions;
            set => SetProperty(ref _mcqOptions, value);
        }

        private string _selectedMcqOption;
        public string SelectedMcqOption
        {
            get => _selectedMcqOption;
            set => SetProperty(ref _selectedMcqOption, value);
        }

        private bool _mcqAnswered;
        public bool McqAnswered
        {
            get => _mcqAnswered;
            set => SetProperty(ref _mcqAnswered, value);
        }

        private bool _mcqWasCorrect;
        public bool McqWasCorrect
        {
            get => _mcqWasCorrect;
            set => SetProperty(ref _mcqWasCorrect, value);
        }

        // --- Typing state ---
        private string _typedAnswer = "";
        public string TypedAnswer
        {
            get => _typedAnswer;
            set
            {
                if (SetProperty(ref _typedAnswer, value))
                {
                    (CheckTypingCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        private bool _typingAnswered;
        public bool TypingAnswered
        {
            get => _typingAnswered;
            set => SetProperty(ref _typingAnswered, value);
        }

        private bool _typingWasCorrect;
        public bool TypingWasCorrect
        {
            get => _typingWasCorrect;
            set => SetProperty(ref _typingWasCorrect, value);
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
        public ICommand SelectMcqOptionCommand { get; }
        public ICommand NextMcqWordCommand { get; }
        public ICommand CheckTypingCommand { get; }
        public ICommand NextTypingWordCommand { get; }
        public ICommand SetExerciseTypeCommand { get; }

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

            SelectMcqOptionCommand = new RelayCommand(
                async (param) =>
                {
                    if (param is string option)
                        await HandleMcqSelectAsync(option);
                },
                (param) => !McqAnswered && CurrentWord != null
            );

            NextMcqWordCommand = new RelayCommand(
                async (param) => await MoveToNextWordAsync(),
                (param) => McqAnswered
            );

            CheckTypingCommand = new RelayCommand(
                async (param) => await HandleCheckTypingAsync(),
                (param) => !TypingAnswered && CurrentWord != null && !string.IsNullOrWhiteSpace(TypedAnswer)
            );

            NextTypingWordCommand = new RelayCommand(
                async (param) => await MoveToNextWordAsync(),
                (param) => TypingAnswered
            );

            SetExerciseTypeCommand = new RelayCommand(
                (param) =>
                {
                    if (param is string type)
                    {
                        ExerciseType = type;
                        IsExerciseTypeChosen = true;
                    }
                }
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

                _allWords = sessionWords.ToList();
                _totalWordsCount = sessionWords.Count;
                OnPropertyChanged(nameof(TotalWordsCount));
                _wordsQueue = new Queue<Word>(sessionWords);
                CurrentWord = _wordsQueue.Peek();
                IsFlipped = false;
                _sessionStopwatch.Start();
                PrepareCurrentExercise();

                (FlipCardCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (AnswerCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
            catch (System.Net.Http.HttpRequestException ex)
            {
                EventAggregator.Instance.Publish(ShowNotificationMessage.Error(
                    "Ошибка загрузки",
                    $"Ошибка загрузки сессии: {ex.Message}"));
                CloseTab(null);
            }
        }

        private void PrepareCurrentExercise()
        {
            if (CurrentWord == null) return;

            McqAnswered = false;
            McqWasCorrect = false;
            SelectedMcqOption = null;
            TypingAnswered = false;
            TypingWasCorrect = false;
            TypedAnswer = "";

            if (ExerciseType == "mcq")
                GenerateMcqOptions();

            (SelectMcqOptionCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (NextMcqWordCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (CheckTypingCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (NextTypingWordCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        private void GenerateMcqOptions()
        {
            var options = new List<string> { CurrentWord.Translation };

            var distractors = _allWords
                .Where(w => w.Id != CurrentWord.Id && !string.IsNullOrEmpty(w.Translation))
                .Select(w => w.Translation)
                .Distinct()
                .OrderBy(_ => _random.Next())
                .Take(3)
                .ToList();

            options.AddRange(distractors);

            var genericOptions = new[] { "слово", "ответ", "перевод", "значение" };
            while (options.Count < 4)
            {
                var option = genericOptions[_random.Next(genericOptions.Length)] + " " + (_random.Next(99) + 1);
                if (!options.Contains(option))
                    options.Add(option);
            }

            McqOptions = new ObservableCollection<string>(options.OrderBy(_ => _random.Next()));
        }

        private async Task HandleMcqSelectAsync(string option)
        {
            if (McqAnswered || CurrentWord == null) return;

            SelectedMcqOption = option;
            McqAnswered = true;
            McqWasCorrect = option.Equals(CurrentWord.Translation, StringComparison.OrdinalIgnoreCase);

            var quality = McqWasCorrect ? ResponseQuality.Good : ResponseQuality.Again;
            await TrackAndSubmitAsync(quality);

            (SelectMcqOptionCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (NextMcqWordCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        private async Task HandleCheckTypingAsync()
        {
            if (TypingAnswered || CurrentWord == null) return;

            TypingAnswered = true;

            var userAnswer = TypedAnswer.Trim().ToLowerInvariant();
            var correctAnswer = CurrentWord.Translation.Trim().ToLowerInvariant();
            TypingWasCorrect = userAnswer == correctAnswer;

            var quality = TypingWasCorrect ? ResponseQuality.Good : ResponseQuality.Again;
            await TrackAndSubmitAsync(quality);

            (CheckTypingCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (NextTypingWordCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        private async Task HandleAnswerAsync(ResponseQuality quality)
        {
            await TrackAndSubmitAsync(quality);

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
            PrepareCurrentExercise();
        }

        private async Task TrackAndSubmitAsync(ResponseQuality quality)
        {
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

            var request = new UpdateProgressRequest
            {
                WordId = CurrentWord.Id,
                Quality = quality
            };

            try
            {
                await _dataService.UpdateProgressAsync(request);
                EventAggregator.Instance.Publish(new RefreshDataMessage());
            }
            catch (System.Net.Http.HttpRequestException ex)
            {
                EventAggregator.Instance.Publish(ShowNotificationMessage.Error(
                    "Ошибка",
                    $"Ошибка сохранения прогресса: {ex.Message}"));
            }
        }

        private async Task MoveToNextWordAsync()
        {
            var wordToRequeue = _wordsQueue.Dequeue();

            if (McqAnswered && !McqWasCorrect || TypingAnswered && !TypingWasCorrect)
            {
                _wordsQueue.Enqueue(wordToRequeue);
            }

            if (!_wordsQueue.Any())
            {
                CompleteSession();
                return;
            }

            CurrentWord = null;
            await Task.Delay(300);
            CurrentWord = _wordsQueue.Peek();
            PrepareCurrentExercise();
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
