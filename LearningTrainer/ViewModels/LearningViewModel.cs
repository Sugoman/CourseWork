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
    public class McqOptionItem : Core.ObservableObject
    {
        public string Text { get; }
        public bool IsCorrect { get; }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        private bool _isRevealed;
        public bool IsRevealed
        {
            get => _isRevealed;
            set => SetProperty(ref _isRevealed, value);
        }

        public McqOptionItem(string text, bool isCorrect)
        {
            Text = text;
            IsCorrect = isCorrect;
        }
    }

    public class LearningViewModel : TabViewModelBase
    {
        private readonly IDataService _dataService;
        private readonly int _dictionaryId;
        private readonly string _dictionaryName;
        private readonly string _languageFrom;
        private readonly SpeechService _speechService;
        private readonly SettingsService _settingsService;
        private Queue<Word> _wordsQueue;
        private List<Word> _allWords = new();
        private readonly Stopwatch _sessionStopwatch = new();
        private int _totalWordsCount;
        private readonly Random _random = new();
        private bool _isProcessing;
        private DateTime _sessionStartTime;
        private bool _isMixedMode;
        private bool _disposed;
        private string _translationDirection = "direct";
        private bool _isAdaptiveDifficulty;

        private Word _currentWord;
        public Word CurrentWord
        {
            get => _currentWord;
            set => SetProperty(ref _currentWord, value);
        }

        // --- Empty session state ---
        private bool _isEmptySession;
        public bool IsEmptySession
        {
            get => _isEmptySession;
            set => SetProperty(ref _isEmptySession, value);
        }

        private bool _hasDictionaryWords;
        public bool HasDictionaryWords
        {
            get => _hasDictionaryWords;
            set => SetProperty(ref _hasDictionaryWords, value);
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
                    OnPropertyChanged(nameof(IsListeningMode));
                    PrepareCurrentExercise();
                }
            }
        }

        public bool IsFlashcardMode => ExerciseType == "flashcard";
        public bool IsMcqMode => ExerciseType == "mcq";
        public bool IsTypingMode => ExerciseType == "typing";
        public bool IsListeningMode => ExerciseType == "listening";

        // --- Translation direction ---
        private bool _isReversed;
        public bool IsReversed
        {
            get => _isReversed;
            set
            {
                if (SetProperty(ref _isReversed, value))
                {
                    OnPropertyChanged(nameof(DisplayedText));
                    OnPropertyChanged(nameof(DisplayedTranscription));
                    OnPropertyChanged(nameof(ExpectedAnswer));
                    OnPropertyChanged(nameof(CorrectAnswerDisplay));
                }
            }
        }

        public string DisplayedText => CurrentWord == null ? "" : (IsReversed ? CurrentWord.Translation : CurrentWord.OriginalWord);
        public string DisplayedTranscription => CurrentWord == null ? "" : (IsReversed ? "" : CurrentWord.Transcription ?? "");
        public string ExpectedAnswer => CurrentWord == null ? "" : (IsReversed ? CurrentWord.OriginalWord : CurrentWord.Translation);
        public string CorrectAnswerDisplay => ExpectedAnswer;

        // --- Error context (#3) ---
        private string _errorContextExample;
        public string ErrorContextExample
        {
            get => _errorContextExample;
            set => SetProperty(ref _errorContextExample, value);
        }

        private string _errorContextTranscription;
        public string ErrorContextTranscription
        {
            get => _errorContextTranscription;
            set => SetProperty(ref _errorContextTranscription, value);
        }

        private bool _showErrorContext;
        public bool ShowErrorContext
        {
            get => _showErrorContext;
            set => SetProperty(ref _showErrorContext, value);
        }

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
        private ObservableCollection<McqOptionItem> _mcqOptions = new();
        public ObservableCollection<McqOptionItem> McqOptions
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

        private bool _typingWasAlmostCorrect;
        public bool TypingWasAlmostCorrect
        {
            get => _typingWasAlmostCorrect;
            set => SetProperty(ref _typingWasAlmostCorrect, value);
        }

        // --- Listening state ---
        private bool _listeningAnswered;
        public bool ListeningAnswered
        {
            get => _listeningAnswered;
            set => SetProperty(ref _listeningAnswered, value);
        }

        private bool _listeningWasCorrect;
        public bool ListeningWasCorrect
        {
            get => _listeningWasCorrect;
            set => SetProperty(ref _listeningWasCorrect, value);
        }

        private bool _listeningWasAlmostCorrect;
        public bool ListeningWasAlmostCorrect
        {
            get => _listeningWasAlmostCorrect;
            set => SetProperty(ref _listeningWasAlmostCorrect, value);
        }

        private string _listeningTypedAnswer = "";
        public string ListeningTypedAnswer
        {
            get => _listeningTypedAnswer;
            set
            {
                if (SetProperty(ref _listeningTypedAnswer, value))
                {
                    (CheckListeningCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        // --- Streak ---
        private int _currentStreak;
        public int CurrentStreak
        {
            get => _currentStreak;
            set
            {
                if (SetProperty(ref _currentStreak, value))
                    OnPropertyChanged(nameof(ShowStreak));
            }
        }

        public bool ShowStreak => CurrentStreak >= 3;

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

        public int CompletedWordsCount => _totalWordsCount - (_wordsQueue?.Count ?? _totalWordsCount);

        public double SessionProgress => _wordsQueue != null && _totalWordsCount > 0
            ? Math.Round((double)CompletedWordsCount / _totalWordsCount * 100.0, 1)
            : 0;

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
        public ICommand StartEarlyCommand { get; }
        public ICommand SelectMcqOptionCommand { get; }
        public ICommand NextMcqWordCommand { get; }
        public ICommand CheckTypingCommand { get; }
        public ICommand NextTypingWordCommand { get; }
        public ICommand SetExerciseTypeCommand { get; }
        public ICommand SpeakWordCommand { get; }
        public ICommand SkipWordCommand { get; }
        public ICommand CopyReportCommand { get; }
        public ICommand SetWordLimitCommand { get; }
        public ICommand ReplayListeningCommand { get; }
        public ICommand CheckListeningCommand { get; }
        public ICommand NextListeningWordCommand { get; }
        public ICommand SetTranslationDirectionCommand { get; }

        private int _selectedWordLimit;
        public int SelectedWordLimit
        {
            get => _selectedWordLimit;
            set => SetProperty(ref _selectedWordLimit, value);
        }

        public LearningViewModel(IDataService dataService, int dictionaryId, string dictionaryName, string languageFrom = "English", SettingsService settingsService = null)
        {
            _dataService = dataService;
            _dictionaryId = dictionaryId;
            _dictionaryName = dictionaryName;
            _languageFrom = languageFrom;
            _settingsService = settingsService;
            _speechService = new SpeechService();

            if (settingsService != null)
            {
                _translationDirection = settingsService.CurrentSettings.DefaultTranslationDirection ?? "direct";
                _isAdaptiveDifficulty = settingsService.CurrentSettings.EnableAdaptiveDifficulty;
                _speechService.Volume = settingsService.CurrentSettings.TtsVolume;
            }

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
                    if (param is McqOptionItem optionItem)
                        await HandleMcqSelectAsync(optionItem);
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
                        if (type == "mixed")
                        {
                            _isMixedMode = true;
                            type = PickRandomExerciseType();
                        }
                        else
                        {
                            _isMixedMode = false;
                            if (type == "mcq" && _allWords != null)
                            {
                                var field = GetMcqDistractorField();
                                var distinctValues = _allWords
                                    .Select(w => field(w))
                                    .Where(t => !string.IsNullOrEmpty(t))
                                    .Distinct()
                                    .Count();
                                if (distinctValues < 4)
                                {
                                    EventAggregator.Instance.Publish(ShowNotificationMessage.Info(
                                        "Недостаточно слов",
                                        "Для режима теста нужно минимум 4 слова с разными переводами. Выбран режим карточек."));
                                    type = "flashcard";
                                }
                            }
                        }
                        // Apply translation direction
                        ApplyTranslationDirection();
                        ExerciseType = type;
                        IsExerciseTypeChosen = true;
                    }
                }
            );

            CloseTabCommand = new RelayCommand(CloseTab);

            SpeakWordCommand = new RelayCommand(
                (param) =>
                {
                    if (CurrentWord != null)
                        _speechService.Speak(CurrentWord.OriginalWord, _languageFrom);
                },
                (param) => CurrentWord != null
            );

            StartEarlyCommand = new RelayCommand(async (param) => await StartEarlyAsync());

            SkipWordCommand = new RelayCommand(
                async (param) => await SkipWordAsync(),
                (param) => CurrentWord != null && !_isProcessing
            );

            CopyReportCommand = new RelayCommand(
                (param) => CopyReportToClipboard(),
                (param) => IsSessionComplete
            );

            SetWordLimitCommand = new RelayCommand(
                (param) =>
                {
                    if (param is string val && int.TryParse(val, out int limit))
                        SelectedWordLimit = limit;
                }
            );

            ReplayListeningCommand = new RelayCommand(
                (param) =>
                {
                    if (CurrentWord != null)
                        _speechService.Speak(CurrentWord.OriginalWord, _languageFrom);
                },
                (param) => CurrentWord != null && IsListeningMode
            );

            CheckListeningCommand = new RelayCommand(
                async (param) => await HandleCheckListeningAsync(),
                (param) => !ListeningAnswered && CurrentWord != null && !string.IsNullOrWhiteSpace(ListeningTypedAnswer)
            );

            NextListeningWordCommand = new RelayCommand(
                async (param) => await MoveToNextWordAsync(),
                (param) => ListeningAnswered
            );

            SetTranslationDirectionCommand = new RelayCommand(
                (param) =>
                {
                    if (param is string dir)
                        _translationDirection = dir;
                }
            );

            _ = LoadSessionAsync();
        }

        private async Task LoadSessionAsync()
        {
            try
            {
                var sessionWords = await _dataService.GetReviewSessionAsync(_dictionaryId);

                if (sessionWords == null || !sessionWords.Any())
                {
                    // Check if the dictionary has words at all (for early start)
                    try
                    {
                        var dict = await _dataService.GetDictionaryByIdAsync(_dictionaryId);
                        HasDictionaryWords = dict?.Words != null && dict.Words.Any();
                    }
                    catch
                    {
                        HasDictionaryWords = false;
                    }

                    IsEmptySession = true;
                    return;
                }

                StartSession(sessionWords);
            }
            catch (System.Net.Http.HttpRequestException ex)
            {
                EventAggregator.Instance.Publish(ShowNotificationMessage.Error(
                    "Ошибка загрузки",
                    $"Ошибка загрузки сессии: {ex.Message}"));
                CloseTab(null);
            }
        }

        private async Task StartEarlyAsync()
        {
            try
            {
                var dict = await _dataService.GetDictionaryByIdAsync(_dictionaryId);
                if (dict?.Words == null || !dict.Words.Any())
                    return;

                IsEmptySession = false;
                StartSession(dict.Words.ToList());
            }
            catch (Exception ex)
            {
                EventAggregator.Instance.Publish(ShowNotificationMessage.Error(
                    "Ошибка",
                    $"Не удалось загрузить слова: {ex.Message}"));
            }
        }

        private void StartSession(List<Word> words)
        {
            _allWords = words.ToList();

            var shuffled = words.OrderBy(_ => _random.Next()).ToList();
            if (SelectedWordLimit > 0 && SelectedWordLimit < shuffled.Count)
                shuffled = shuffled.Take(SelectedWordLimit).ToList();

            _totalWordsCount = shuffled.Count;
            OnPropertyChanged(nameof(TotalWordsCount));

            _wordsQueue = new Queue<Word>(shuffled);
            CurrentWord = _wordsQueue.Peek();
            IsFlipped = false;
            _sessionStartTime = DateTime.UtcNow;
            _sessionStopwatch.Start();
            UpdateProgress();
            PrepareCurrentExercise();

            (FlipCardCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (AnswerCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (SpeakWordCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        private void PrepareCurrentExercise()
        {
            if (CurrentWord == null) return;

            // Apply direction for each word in random mode
            if (_translationDirection == "random")
                IsReversed = _random.Next(2) == 0;

            if (_isMixedMode)
            {
                var newType = _isAdaptiveDifficulty ? PickAdaptiveExerciseType() : PickRandomExerciseType();
                if (newType != _exerciseType)
                {
                    _exerciseType = newType;
                    OnPropertyChanged(nameof(ExerciseType));
                    OnPropertyChanged(nameof(IsFlashcardMode));
                    OnPropertyChanged(nameof(IsMcqMode));
                    OnPropertyChanged(nameof(IsTypingMode));
                    OnPropertyChanged(nameof(IsListeningMode));
                }
            }

            // Notify direction-dependent properties
            OnPropertyChanged(nameof(DisplayedText));
            OnPropertyChanged(nameof(DisplayedTranscription));
            OnPropertyChanged(nameof(ExpectedAnswer));
            OnPropertyChanged(nameof(CorrectAnswerDisplay));

            // Reset error context
            ShowErrorContext = false;
            ErrorContextExample = null;
            ErrorContextTranscription = null;

            McqAnswered = false;
            McqWasCorrect = false;
            SelectedMcqOption = null;
            TypingAnswered = false;
            TypingWasCorrect = false;
            TypingWasAlmostCorrect = false;
            TypedAnswer = "";
            ListeningAnswered = false;
            ListeningWasCorrect = false;
            ListeningWasAlmostCorrect = false;
            ListeningTypedAnswer = "";

            if (ExerciseType == "mcq")
                GenerateMcqOptions();

            // Auto-play in listening mode
            if (ExerciseType == "listening" && CurrentWord != null)
                _speechService.Speak(CurrentWord.OriginalWord, _languageFrom);

            (SelectMcqOptionCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (NextMcqWordCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (CheckTypingCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (NextTypingWordCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (CheckListeningCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (NextListeningWordCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (ReplayListeningCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (SpeakWordCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (SkipWordCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        private Func<Word, string> GetMcqDistractorField()
        {
            return IsReversed ? (w => w.OriginalWord) : (w => w.Translation);
        }

        private void GenerateMcqOptions()
        {
            var correctAnswer = ExpectedAnswer;
            var distractorField = GetMcqDistractorField();
            var options = new List<string> { correctAnswer };

            var distractors = _allWords
                .Where(w => w.Id != CurrentWord.Id && !string.IsNullOrEmpty(distractorField(w)))
                .Select(w => distractorField(w))
                .Distinct()
                .OrderBy(_ => _random.Next())
                .Take(3)
                .ToList();

            options.AddRange(distractors);

            if (options.Count < 4)
            {
                _exerciseType = "flashcard";
                OnPropertyChanged(nameof(ExerciseType));
                OnPropertyChanged(nameof(IsFlashcardMode));
                OnPropertyChanged(nameof(IsMcqMode));
                OnPropertyChanged(nameof(IsTypingMode));
                EventAggregator.Instance.Publish(ShowNotificationMessage.Info(
                    GetLocalized("Loc.Learning.NotEnoughWords") is string t && t != "Loc.Learning.NotEnoughWords" ? t : "Недостаточно слов",
                    GetLocalized("Loc.Learning.McqRequires4") is string m && m != "Loc.Learning.McqRequires4" ? m : "Для режима теста нужно минимум 4 слова с разными переводами. Переключено на карточки."));
                return;
            }

            McqOptions = new ObservableCollection<McqOptionItem>(
                options.OrderBy(_ => _random.Next())
                       .Select(o => new McqOptionItem(o, o.Equals(correctAnswer, StringComparison.OrdinalIgnoreCase)))
            );
        }

        private async Task HandleMcqSelectAsync(McqOptionItem selectedItem)
        {
            if (_isProcessing || McqAnswered || CurrentWord == null) return;
            _isProcessing = true;
            try
            {
                selectedItem.IsSelected = true;
                McqAnswered = true;
                McqWasCorrect = selectedItem.IsCorrect;
                SelectedMcqOption = selectedItem.Text;

                foreach (var option in McqOptions)
                    option.IsRevealed = true;

                var quality = McqWasCorrect ? ResponseQuality.Good : ResponseQuality.Again;
                await TrackAndSubmitAsync(quality);

                (SelectMcqOptionCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (NextMcqWordCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
            finally
            {
                _isProcessing = false;
            }
        }

        private async Task HandleCheckTypingAsync()
        {
            if (_isProcessing || TypingAnswered || CurrentWord == null) return;
            _isProcessing = true;
            try
            {
                TypingAnswered = true;

                var userAnswer = TypedAnswer.Trim().ToLowerInvariant();
                var correctAnswer = ExpectedAnswer.Trim().ToLowerInvariant();

                if (userAnswer == correctAnswer)
                {
                    TypingWasCorrect = true;
                    TypingWasAlmostCorrect = false;
                    await TrackAndSubmitAsync(ResponseQuality.Good);
                }
                else
                {
                    int distance = LevenshteinDistance(userAnswer, correctAnswer);
                    int threshold = Math.Max(1, correctAnswer.Length / 5);
                    bool isAlmostCorrect = distance > 0 && distance <= threshold && correctAnswer.Length >= 3;

                    if (isAlmostCorrect)
                    {
                        TypingWasCorrect = false;
                        TypingWasAlmostCorrect = true;
                        await TrackAndSubmitAsync(ResponseQuality.Hard);
                    }
                    else
                    {
                        TypingWasCorrect = false;
                        TypingWasAlmostCorrect = false;
                        await TrackAndSubmitAsync(ResponseQuality.Again);
                    }
                }

                (CheckTypingCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (NextTypingWordCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
            finally
            {
                _isProcessing = false;
            }
        }

        private async Task HandleAnswerAsync(ResponseQuality quality)
        {
            if (_isProcessing) return;
            _isProcessing = true;
            try
            {
                await TrackAndSubmitAsync(quality);

                var wordToRequeue = _wordsQueue.Dequeue();

                if (quality == ResponseQuality.Again || quality == ResponseQuality.Hard)
                {
                    RequeueWordAtAdaptivePosition(wordToRequeue);
                }

                UpdateProgress();

                if (!_wordsQueue.Any())
                {
                    await CompleteSessionAsync();
                    return;
                }

                CurrentWord = null;
                IsFlipped = false;

                await Task.Delay(450);

                CurrentWord = _wordsQueue.Peek();
                PrepareCurrentExercise();
            }
            finally
            {
                _isProcessing = false;
            }
        }

        private async Task TrackAndSubmitAsync(ResponseQuality quality)
        {
            if (quality >= ResponseQuality.Good)
            {
                CorrectCount++;
                CurrentStreak++;
            }
            else
            {
                WrongCount++;
                CurrentStreak = 0;

                // Populate error context (#3)
                ErrorContextExample = CurrentWord.Example;
                ErrorContextTranscription = CurrentWord.Transcription;
                ShowErrorContext = !string.IsNullOrEmpty(ErrorContextExample) || !string.IsNullOrEmpty(ErrorContextTranscription);

                if (!DifficultWords.Any(w => w.WordId == CurrentWord.Id))
                {
                    DifficultWords.Add(new WordResultDto
                    {
                        WordId = CurrentWord.Id,
                        OriginalWord = CurrentWord.OriginalWord,
                        Quality = quality
                    });
                }
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
            if (_isProcessing) return;
            _isProcessing = true;
            try
            {
                var wordToRequeue = _wordsQueue.Dequeue();

                if (McqAnswered && !McqWasCorrect || TypingAnswered && !TypingWasCorrect || ListeningAnswered && !ListeningWasCorrect)
                {
                    RequeueWordAtAdaptivePosition(wordToRequeue);
                }

                UpdateProgress();

                if (!_wordsQueue.Any())
                {
                    await CompleteSessionAsync();
                    return;
                }

                CurrentWord = null;
                await Task.Delay(300);
                CurrentWord = _wordsQueue.Peek();
                PrepareCurrentExercise();
            }
            finally
            {
                _isProcessing = false;
            }
        }

        private async Task CompleteSessionAsync()
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
                ResultEmoji = "★";
                ResultMessage = "Превосходно!";
            }
            else if (AccuracyPercent >= 70)
            {
                ResultEmoji = "✦";
                ResultMessage = "Отличная работа!";
            }
            else if (AccuracyPercent >= 50)
            {
                ResultEmoji = "●";
                ResultMessage = "Хороший результат!";
            }
            else
            {
                ResultEmoji = "▲";
                ResultMessage = "Продолжайте тренироваться!";
            }

            try
            {
                await _dataService.SaveTrainingSessionAsync(
                    _sessionStartTime,
                    DateTime.UtcNow,
                    _totalWordsCount,
                    CorrectCount,
                    WrongCount,
                    ExerciseType,
                    _dictionaryId);
            }
            catch { }

            IsSessionComplete = true;
        }

        private async Task SkipWordAsync()
        {
            if (_isProcessing || CurrentWord == null) return;
            _isProcessing = true;
            try
            {
                await TrackAndSubmitAsync(ResponseQuality.Again);

                var wordToRequeue = _wordsQueue.Dequeue();
                RequeueWordAtAdaptivePosition(wordToRequeue);

                UpdateProgress();

                if (!_wordsQueue.Any())
                {
                    await CompleteSessionAsync();
                    return;
                }

                CurrentWord = null;
                IsFlipped = false;
                await Task.Delay(200);
                CurrentWord = _wordsQueue.Peek();
                PrepareCurrentExercise();
            }
            finally
            {
                _isProcessing = false;
            }
        }

        private void RequeueWordAtAdaptivePosition(Word word)
        {
            var items = _wordsQueue.ToList();
            int position;

            if (_isAdaptiveDifficulty)
            {
                // Use progress history for smarter requeue positioning
                var progress = word.Progress?.FirstOrDefault();
                if (progress != null)
                {
                    double successRate = progress.SuccessRate;
                    if (successRate < 0.5)
                        position = Math.Min(items.Count, _random.Next(2, 5));
                    else if (successRate < 0.7)
                        position = Math.Min(items.Count, _random.Next(4, 7));
                    else
                        position = Math.Min(items.Count, _random.Next(6, 11));
                }
                else
                {
                    position = Math.Min(items.Count, _random.Next(2, 5));
                }
            }
            else
            {
                position = Math.Min(items.Count, _random.Next(3, 8));
            }

            items.Insert(position, word);
            _wordsQueue = new Queue<Word>(items);
        }

        private void UpdateProgress()
        {
            OnPropertyChanged(nameof(CompletedWordsCount));
            OnPropertyChanged(nameof(SessionProgress));
        }

        private static int LevenshteinDistance(string s, string t)
        {
            if (string.IsNullOrEmpty(s)) return t?.Length ?? 0;
            if (string.IsNullOrEmpty(t)) return s.Length;

            var d = new int[s.Length + 1, t.Length + 1];
            for (int i = 0; i <= s.Length; i++) d[i, 0] = i;
            for (int j = 0; j <= t.Length; j++) d[0, j] = j;

            for (int i = 1; i <= s.Length; i++)
            {
                for (int j = 1; j <= t.Length; j++)
                {
                    int cost = s[i - 1] == t[j - 1] ? 0 : 1;
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost);
                }
            }
            return d[s.Length, t.Length];
        }

        private string PickRandomExerciseType()
        {
            var types = new List<string> { "flashcard", "typing" };
            var distractorField = GetMcqDistractorField();
            if (_allWords != null && _allWords.Select(w => distractorField(w)).Where(t => !string.IsNullOrEmpty(t)).Distinct().Count() >= 4)
                types.Add("mcq");
            types.Add("listening");
            return types[_random.Next(types.Count)];
        }

        /// <summary>
        /// Picks exercise type based on word's KnowledgeLevel (#4 - adaptive difficulty).
        /// Low level → flashcard, mid → MCQ, high → typing.
        /// </summary>
        private string PickAdaptiveExerciseType()
        {
            var progress = CurrentWord?.Progress?.FirstOrDefault();
            int level = progress?.KnowledgeLevel ?? 0;

            if (level <= 2)
                return "flashcard";

            var distractorField = GetMcqDistractorField();
            bool canMcq = _allWords != null && _allWords.Select(w => distractorField(w)).Where(t => !string.IsNullOrEmpty(t)).Distinct().Count() >= 4;

            if (level <= 4)
                return canMcq ? "mcq" : "flashcard";

            // High level: prefer typing or listening
            if (_random.Next(2) == 0)
                return "listening";

            return "typing";
        }

        private void ApplyTranslationDirection()
        {
            IsReversed = _translationDirection switch
            {
                "reverse" => true,
                "random" => _random.Next(2) == 0,
                _ => false
            };
        }

        /// <summary>
        /// Handles checking the listening answer — user typed what they heard.
        /// </summary>
        private async Task HandleCheckListeningAsync()
        {
            if (_isProcessing || ListeningAnswered || CurrentWord == null) return;
            _isProcessing = true;
            try
            {
                ListeningAnswered = true;

                var userAnswer = ListeningTypedAnswer.Trim().ToLowerInvariant();
                var correctAnswer = CurrentWord.OriginalWord.Trim().ToLowerInvariant();

                if (userAnswer == correctAnswer)
                {
                    ListeningWasCorrect = true;
                    ListeningWasAlmostCorrect = false;
                    await TrackAndSubmitAsync(ResponseQuality.Good);
                }
                else
                {
                    int distance = LevenshteinDistance(userAnswer, correctAnswer);
                    int threshold = Math.Max(1, correctAnswer.Length / 5);
                    bool isAlmostCorrect = distance > 0 && distance <= threshold && correctAnswer.Length >= 3;

                    if (isAlmostCorrect)
                    {
                        ListeningWasCorrect = false;
                        ListeningWasAlmostCorrect = true;
                        await TrackAndSubmitAsync(ResponseQuality.Hard);
                    }
                    else
                    {
                        ListeningWasCorrect = false;
                        ListeningWasAlmostCorrect = false;
                        await TrackAndSubmitAsync(ResponseQuality.Again);
                    }
                }

                (CheckListeningCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (NextListeningWordCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
            finally
            {
                _isProcessing = false;
            }
        }

        private void CopyReportToClipboard()
        {
            var errors = DifficultWords.Any()
                ? string.Join(", ", DifficultWords.Select(w => w.OriginalWord))
                : "нет";

            var report = $"📚 Тренировка: {_dictionaryName}\n" +
                         $"📅 Дата: {DateTime.Now:dd.MM.yyyy HH:mm}\n" +
                         $"🎯 Точность: {AccuracyPercent:0}% ({CorrectCount}/{_totalWordsCount})\n" +
                         $"⏱ Время: {SessionDuration}\n" +
                         $"❌ Ошибки: {errors}";

            try
            {
                Clipboard.SetText(report);
                EventAggregator.Instance.Publish(ShowNotificationMessage.Success(
                    "Скопировано", "Отчёт скопирован в буфер обмена"));
            }
            catch { }
        }

        public override void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _speechService?.Dispose();
            base.Dispose();
        }

        private void CloseTab(object parameter)
        {
            Dispose();
            EventAggregator.Instance.Publish(new CloseTabMessage(this));
        }
    }
}
