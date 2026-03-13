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

    /// <summary>
    /// Item for Matching mode (§18.1a). Represents one side of a match pair.
    /// </summary>
    public class MatchItem : Core.ObservableObject
    {
        public int WordId { get; set; }
        public string Text { get; set; }
        public bool IsOriginal { get; set; }

        private bool _isMatched;
        public bool IsMatched
        {
            get => _isMatched;
            set => SetProperty(ref _isMatched, value);
        }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        private bool _isWrong;
        public bool IsWrong
        {
            get => _isWrong;
            set => SetProperty(ref _isWrong, value);
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
        private Dictionary<int, string?> _userNotes = new();
        private Dictionary<int, string> _errorModes = new();
        private DateTime _sessionStartTime;
        private bool _isMixedMode;
        private bool _disposed;
        private string _translationDirection = "direct";
        private bool _isAdaptiveDifficulty;

        // === Speed Round (§18.1d) ===
        private System.Windows.Threading.DispatcherTimer _speedTimer;
        private int _speedRoundTimeLeft;
        private int _speedRoundScore;
        private bool _isSpeedRound;

        // === Matching mode (§18.1a) ===
        private List<MatchItem> _matchItems = new();
        private MatchItem _selectedMatchItem;
        private bool _isMatchAnimating;

        // === Milestone Toast (§18.5) ===
        private HashSet<string> _previouslyUnlockedAchievements = new();

        // === Pomodoro Break (§18.1) ===
        private const int PomodoroWordInterval = 25;
        private int _wordsSinceLastBreak;

        private bool _showPomodoroBreak;
        public bool ShowPomodoroBreak
        {
            get => _showPomodoroBreak;
            set => SetProperty(ref _showPomodoroBreak, value);
        }

        public string PomodoroMessage => $"Вы прошли {PomodoroWordInterval} слов! Сделайте короткий перерыв 🍅";

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
                    OnPropertyChanged(nameof(IsMatchingMode));
                    OnPropertyChanged(nameof(IsClozeMode));
                    OnPropertyChanged(nameof(IsSpellingBeeMode));
                    PrepareCurrentExercise();
                }
            }
        }

        public bool IsFlashcardMode => ExerciseType == "flashcard";
        public bool IsMcqMode => ExerciseType == "mcq";
        public bool IsTypingMode => ExerciseType == "typing";
        public bool IsListeningMode => ExerciseType == "listening";
        public bool IsMatchingMode => ExerciseType == "matching";

        // === Speed Round (§18.1d) ===
        public bool IsSpeedRound
        {
            get => _isSpeedRound;
            set
            {
                if (SetProperty(ref _isSpeedRound, value))
                    OnPropertyChanged(nameof(ShowSpeedOverlay));
            }
        }

        public int SpeedRoundTimeLeft
        {
            get => _speedRoundTimeLeft;
            set => SetProperty(ref _speedRoundTimeLeft, value);
        }

        public int SpeedRoundScore
        {
            get => _speedRoundScore;
            set => SetProperty(ref _speedRoundScore, value);
        }

        public bool ShowSpeedOverlay => IsSpeedRound && IsExerciseTypeChosen;

        // === Matching mode (§18.1a) ===
        public ObservableCollection<MatchItem> MatchItems { get; } = new();

        private int _matchedPairsCount;
        public int MatchedPairsCount
        {
            get => _matchedPairsCount;
            set => SetProperty(ref _matchedPairsCount, value);
        }

        private int _matchTotalPairs;
        public int MatchTotalPairs
        {
            get => _matchTotalPairs;
            set => SetProperty(ref _matchTotalPairs, value);
        }

        private bool _matchingCompleted;
        public bool MatchingCompleted
        {
            get => _matchingCompleted;
            set => SetProperty(ref _matchingCompleted, value);
        }

        // === Cloze mode (§18.1b) ===
        private string _clozeSentence = "";
        public string ClozeSentence
        {
            get => _clozeSentence;
            set => SetProperty(ref _clozeSentence, value);
        }

        private string _clozeAnswer = "";
        public string ClozeAnswer
        {
            get => _clozeAnswer;
            set
            {
                if (SetProperty(ref _clozeAnswer, value))
                    (CheckClozeCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        private bool _clozeAnswered;
        public bool ClozeAnswered
        {
            get => _clozeAnswered;
            set => SetProperty(ref _clozeAnswered, value);
        }

        private bool _clozeWasCorrect;
        public bool ClozeWasCorrect
        {
            get => _clozeWasCorrect;
            set => SetProperty(ref _clozeWasCorrect, value);
        }

        private string _clozeCorrectWord = "";
        public string ClozeCorrectWord
        {
            get => _clozeCorrectWord;
            set => SetProperty(ref _clozeCorrectWord, value);
        }

        public bool IsClozeMode => ExerciseType == "cloze";
        public bool IsSpellingBeeMode => ExerciseType == "spellingbee";

        // === Spelling Bee (§18.1c) ===
        private string _spellingAnswer = "";
        public string SpellingAnswer
        {
            get => _spellingAnswer;
            set
            {
                if (SetProperty(ref _spellingAnswer, value))
                    (CheckSpellingCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        private bool _spellingAnswered;
        public bool SpellingAnswered
        {
            get => _spellingAnswered;
            set => SetProperty(ref _spellingAnswered, value);
        }

        private bool _spellingWasCorrect;
        public bool SpellingWasCorrect
        {
            get => _spellingWasCorrect;
            set => SetProperty(ref _spellingWasCorrect, value);
        }

        private string _spellingHint = "";
        public string SpellingHint
        {
            get => _spellingHint;
            set => SetProperty(ref _spellingHint, value);
        }

        // === Mastery Indicator (§18.1 LEARNING_IMPROVEMENTS) ===
        public int CurrentWordKnowledgeLevel =>
            CurrentWord?.Progress?.FirstOrDefault()?.KnowledgeLevel ?? 0;

        public string MasteryStars
        {
            get
            {
                int level = CurrentWordKnowledgeLevel;
                int stars = level switch
                {
                    0 => 0,
                    <= 2 => 1,
                    <= 4 => 2,
                    <= 6 => 3,
                    <= 8 => 4,
                    _ => 5
                };
                return new string('⭐', stars) + new string('☆', 5 - stars);
            }
        }

        public string MasteryLevelText => CurrentWordKnowledgeLevel switch
        {
            0 => "Новое",
            <= 2 => "Начинающий",
            <= 4 => "Знакомое",
            <= 6 => "Уверенный",
            <= 8 => "Продвинутый",
            _ => "Освоено"
        };

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

        // --- Error Review Round (#8, §1.2 Error Replay Queue) ---
        private bool _isErrorReviewRound;
        public bool IsErrorReviewRound
        {
            get => _isErrorReviewRound;
            set => SetProperty(ref _isErrorReviewRound, value);
        }

        private string _errorReplayBanner = "";
        public string ErrorReplayBanner
        {
            get => _errorReplayBanner;
            set => SetProperty(ref _errorReplayBanner, value);
        }

        public bool HasDifficultWords => DifficultWords.Count > 0;

        // --- Hint System (#5) ---
        private int _hintCount;
        private string _hintText = "";
        public string HintText
        {
            get => _hintText;
            set => SetProperty(ref _hintText, value);
        }

        private bool _hintUsed;
        public bool HintUsed
        {
            get => _hintUsed;
            set => SetProperty(ref _hintUsed, value);
        }

        // --- Typing Diff (#16) ---
        private string _typingDiffCorrect = "";
        public string TypingDiffCorrect
        {
            get => _typingDiffCorrect;
            set => SetProperty(ref _typingDiffCorrect, value);
        }

        private string _typingDiffUser = "";
        public string TypingDiffUser
        {
            get => _typingDiffUser;
            set => SetProperty(ref _typingDiffUser, value);
        }

        public ICommand FlipCardCommand { get; private set; }
        public ICommand AnswerCommand { get; private set; }
        public ICommand CloseTabCommand { get; private set; }
        public ICommand StartEarlyCommand { get; private set; }
        public ICommand SelectMcqOptionCommand { get; private set; }
        public ICommand NextMcqWordCommand { get; private set; }
        public ICommand CheckTypingCommand { get; private set; }
        public ICommand NextTypingWordCommand { get; private set; }
        public ICommand SetExerciseTypeCommand { get; private set; }
        public ICommand SpeakWordCommand { get; private set; }
        public ICommand SkipWordCommand { get; private set; }
        public ICommand CopyReportCommand { get; private set; }
        public ICommand SetWordLimitCommand { get; private set; }
        public ICommand ReplayListeningCommand { get; private set; }
        public ICommand CheckListeningCommand { get; private set; }
        public ICommand NextListeningWordCommand { get; private set; }
        public ICommand SetTranslationDirectionCommand { get; private set; }
        public ICommand ReviewErrorsCommand { get; private set; }
        public ICommand UseHintCommand { get; private set; }
        public ICommand StartSpeedRoundCommand { get; private set; }
        public ICommand SelectMatchItemCommand { get; private set; }
        public ICommand NextMatchingRoundCommand { get; private set; }
        public ICommand CheckClozeCommand { get; private set; }
        public ICommand NextClozeWordCommand { get; private set; }
        public ICommand CheckSpellingCommand { get; private set; }
        public ICommand NextSpellingWordCommand { get; private set; }
        public ICommand DismissBreakCommand { get; private set; }

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
            InitializeCommands();
            _ = LoadSessionAsync();
        }

        /// <summary>
        /// Constructor for Daily Plan smart training (§18.2 LEARNING_IMPROVEMENTS).
        /// Accepts pre-loaded words from DailyPlan instead of loading from a single dictionary.
        /// </summary>
        public LearningViewModel(IDataService dataService, List<TrainingWordDto> trainingWords, string sessionTitle, SettingsService settingsService = null)
        {
            _dataService = dataService;
            _dictionaryId = 0;
            _dictionaryName = sessionTitle;
            _languageFrom = trainingWords.FirstOrDefault()?.LanguageFrom ?? "English";
            _settingsService = settingsService;
            _speechService = new SpeechService();

            if (settingsService != null)
            {
                _translationDirection = settingsService.CurrentSettings.DefaultTranslationDirection ?? "direct";
                _isAdaptiveDifficulty = settingsService.CurrentSettings.EnableAdaptiveDifficulty;
                _speechService.Volume = settingsService.CurrentSettings.TtsVolume;
            }

            SetLocalizedTitle("Loc.Tab.Learning", $": {sessionTitle}");
            InitializeCommands();

            // Convert TrainingWordDto → Word and start session immediately
            _userNotes = trainingWords
                .Where(w => w.UserNote != null)
                .ToDictionary(w => w.WordId, w => w.UserNote);

            var words = trainingWords.Select(w => new Word
            {
                Id = w.WordId,
                OriginalWord = w.OriginalWord,
                Translation = w.Translation,
                Transcription = w.Transcription,
                Example = w.Example,
                DictionaryId = w.DictionaryId
            }).ToList();

            if (words.Any())
                StartSession(words);
            else
                IsEmptySession = true;
        }

        private void InitializeCommands()
        {
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
                            if (type == "matching" && (_allWords == null || _allWords.Count < 4))
                            {
                                EventAggregator.Instance.Publish(ShowNotificationMessage.Info(
                                    "Недостаточно слов",
                                    "Для режима «Сопоставление» нужно минимум 4 слова. Выбран режим карточек."));
                                type = "flashcard";
                            }
                            if (type == "cloze")
                            {
                                var wordsWithExamples = _allWords?.Count(w => !string.IsNullOrWhiteSpace(w.Example)) ?? 0;
                                if (wordsWithExamples == 0)
                                {
                                    EventAggregator.Instance.Publish(ShowNotificationMessage.Info(
                                        "Нет примеров",
                                        "Для режима «Заполни пропуск» нужны слова с примерами. Выбран режим карточек."));
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

            ReviewErrorsCommand = new RelayCommand(
                (param) => StartErrorReviewRound(),
                (param) => IsSessionComplete && DifficultWords.Count > 0
            );

            UseHintCommand = new RelayCommand(
                (param) => UseHint(),
                (param) => CurrentWord != null
                    && !TypingAnswered && !ListeningAnswered && !ClozeAnswered && !SpellingAnswered
                    && _hintCount < 4
            );

            // Speed Round (§18.1d)
            StartSpeedRoundCommand = new RelayCommand(
                (param) => StartSpeedRound(),
                (param) => !IsSpeedRound && _allWords != null && _allWords.Count >= 4
            );

            // Matching mode (§18.1a)
            SelectMatchItemCommand = new RelayCommand(
                (param) =>
                {
                    if (param is MatchItem item)
                        HandleMatchSelection(item);
                },
                (param) => param is MatchItem m && !m.IsMatched
            );

            NextMatchingRoundCommand = new RelayCommand(
                async (param) => await MoveToNextWordAsync(),
                (param) => MatchingCompleted
            );

            // Cloze mode (§18.1b)
            CheckClozeCommand = new RelayCommand(
                async (param) => await HandleCheckClozeAsync(),
                (param) => !ClozeAnswered && CurrentWord != null && !string.IsNullOrWhiteSpace(ClozeAnswer)
            );

            NextClozeWordCommand = new RelayCommand(
                async (param) => await MoveToNextWordAsync(),
                (param) => ClozeAnswered
            );

            // Spelling Bee (§18.1c)
            CheckSpellingCommand = new RelayCommand(
                async (param) => await HandleCheckSpellingAsync(),
                (param) => !SpellingAnswered && CurrentWord != null && !string.IsNullOrWhiteSpace(SpellingAnswer)
            );

            NextSpellingWordCommand = new RelayCommand(
                async (param) => await MoveToNextWordAsync(),
                (param) => SpellingAnswered
            );

            // Pomodoro Break (§18.1)
            DismissBreakCommand = new RelayCommand(_ =>
            {
                ShowPomodoroBreak = false;
                _wordsSinceLastBreak = 0;
            });
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

            // Capture current achievements for milestone detection (§18.5)
            _ = CaptureCurrentAchievementsAsync();

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

            // Update mastery indicator (§18.1)
            OnPropertyChanged(nameof(CurrentWordKnowledgeLevel));
            OnPropertyChanged(nameof(MasteryStars));
            OnPropertyChanged(nameof(MasteryLevelText));

            // Apply direction for each word in random mode
            if (_translationDirection == "random")
                IsReversed = _random.Next(2) == 0;

            if (_isMixedMode || IsErrorReviewRound)
            {
                string newType;
                if (IsErrorReviewRound && CurrentWord != null && _errorModes.TryGetValue(CurrentWord.Id, out var errMode))
                    newType = GetAlternativeMode(errMode);
                else if (_isMixedMode)
                    newType = _isAdaptiveDifficulty ? PickAdaptiveExerciseType() : PickRandomExerciseType();
                else
                    newType = _exerciseType;

                if (newType != _exerciseType)
                {
                    _exerciseType = newType;
                    OnPropertyChanged(nameof(ExerciseType));
                    OnPropertyChanged(nameof(IsFlashcardMode));
                    OnPropertyChanged(nameof(IsMcqMode));
                    OnPropertyChanged(nameof(IsTypingMode));
                    OnPropertyChanged(nameof(IsListeningMode));
                    OnPropertyChanged(nameof(IsMatchingMode));
                    OnPropertyChanged(nameof(IsClozeMode));
                    OnPropertyChanged(nameof(IsSpellingBeeMode));
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

            // Reset hints and diff
            ResetHint();
            TypingDiffCorrect = "";
            TypingDiffUser = "";

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
            ClozeAnswered = false;
            ClozeWasCorrect = false;
            ClozeAnswer = "";
            ClozeSentence = "";
            ClozeCorrectWord = "";
            SpellingAnswered = false;
            SpellingWasCorrect = false;
            SpellingAnswer = "";
            SpellingHint = "";

            if (ExerciseType == "mcq")
                GenerateMcqOptions();

            if (ExerciseType == "matching")
                GenerateMatchingBoard();

            if (ExerciseType == "cloze")
                GenerateClozeSentence();

            if (ExerciseType == "spellingbee")
                PrepareSpellingBee();

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
            (UseHintCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (StartSpeedRoundCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (SelectMatchItemCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (NextMatchingRoundCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (CheckClozeCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (NextClozeWordCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (CheckSpellingCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (NextSpellingWordCommand as RelayCommand)?.RaiseCanExecuteChanged();
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

                if (IsSpeedRound)
                {
                    AddSpeedScore(McqWasCorrect);
                    // Auto-advance in speed round
                    await Task.Delay(300);
                    _isProcessing = false;
                    await MoveToNextWordAsync();
                    return;
                }

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
                    await TrackAndSubmitAsync(HintUsed ? ResponseQuality.Hard : ResponseQuality.Good);
                }
                else
                {
                    int distance = LevenshteinDistance(userAnswer, correctAnswer);
                    int threshold = Math.Max(1, correctAnswer.Length / 5);
                    bool isAlmostCorrect = distance > 0 && distance <= threshold && correctAnswer.Length >= 3;

                    // Build character-level diff (#16)
                    BuildTypingDiff(userAnswer, correctAnswer);

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

                // §1.2 Error Replay Queue: record the mode where the error occurred
                _errorModes[CurrentWord.Id] = _exerciseType;

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
                // In matching mode, skip multiple words from the queue
                if (IsMatchingMode && MatchingCompleted)
                {
                    for (int i = 0; i < MatchTotalPairs && _wordsQueue.Count > 0; i++)
                        _wordsQueue.Dequeue();
                }
                else
                {
                    var wordToRequeue = _wordsQueue.Dequeue();

                    if (McqAnswered && !McqWasCorrect || TypingAnswered && !TypingWasCorrect || ListeningAnswered && !ListeningWasCorrect || ClozeAnswered && !ClozeWasCorrect || SpellingAnswered && !SpellingWasCorrect)
                    {
                        RequeueWordAtAdaptivePosition(wordToRequeue);
                    }
                }

                UpdateProgress();

                // Pomodoro break check (§18.1)
                _wordsSinceLastBreak++;
                if (_wordsSinceLastBreak >= PomodoroWordInterval && _wordsQueue.Any())
                {
                    ShowPomodoroBreak = true;
                }

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

            // Check for newly unlocked achievements (§18.5 Milestone Toast)
            _ = CheckNewAchievementsAsync();

            // §1.2 Error Replay Queue: auto-start error replay if there are difficult words
            if (DifficultWords.Count > 0 && !IsErrorReviewRound)
            {
                StartErrorReviewRound();
                return;
            }

            IsSessionComplete = true;
            IsErrorReviewRound = false;
            _errorModes.Clear();
            OnPropertyChanged(nameof(HasDifficultWords));
            (ReviewErrorsCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (CopyReportCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        // ── §18.5 Milestone Toast — Achievement notifications ────────

        private async Task CaptureCurrentAchievementsAsync()
        {
            try
            {
                var stats = await _dataService.GetStatisticsAsync("all");
                if (stats?.Achievements != null)
                {
                    _previouslyUnlockedAchievements = new HashSet<string>(
                        stats.Achievements.Where(a => a.IsUnlocked).Select(a => a.Id));
                }
            }
            catch { }
        }

        private async Task CheckNewAchievementsAsync()
        {
            try
            {
                var stats = await _dataService.GetStatisticsAsync("all");
                if (stats?.Achievements == null) return;

                var newlyUnlocked = stats.Achievements
                    .Where(a => a.IsUnlocked && !_previouslyUnlockedAchievements.Contains(a.Id))
                    .ToList();

                foreach (var achievement in newlyUnlocked)
                {
                    EventAggregator.Instance.Publish(ShowNotificationMessage.Success(
                        $"🏆 Достижение: {achievement.Title}",
                        $"{achievement.Icon} {achievement.Description}"));
                }

                // Update the set for potential subsequent checks
                foreach (var a in newlyUnlocked)
                    _previouslyUnlockedAchievements.Add(a.Id);
            }
            catch { }
        }

        // ── #8 Error Review Round ────────────────────────────────────

        private void StartErrorReviewRound()
        {
            if (DifficultWords.Count == 0) return;

            IsSessionComplete = false;
            IsErrorReviewRound = true;

            // Build queue from difficult words
            var errorWords = _allWords
                .Where(w => DifficultWords.Any(d => d.WordId == w.Id))
                .ToList();

            if (errorWords.Count == 0) return;

            // §1.2 Error Replay Queue: show banner with word count
            ErrorReplayBanner = $"🔁 Работа над ошибками: {errorWords.Count} {GetWordDeclension(errorWords.Count)}";

            _wordsQueue = new Queue<Word>(errorWords.OrderBy(_ => _random.Next()));
            _totalWordsCount = _wordsQueue.Count;
            CorrectCount = 0;
            WrongCount = 0;
            DifficultWords.Clear();

            // §1.2: use alternative modes per word instead of forcing flashcard
            IsExerciseTypeChosen = true;

            CurrentWord = _wordsQueue.Peek();
            IsFlipped = false;
            ApplyTranslationDirection();
            PrepareCurrentExercise();

            _sessionStopwatch.Restart();
        }

        /// <summary>
        /// §1.2 Error Replay Queue: maps the mode where the error occurred to an alternative mode.
        /// </summary>
        private static string GetAlternativeMode(string mode) => mode switch
        {
            "mcq" => "typing",
            "typing" => "flashcard",
            "cloze" => "listening",
            "matching" => "cloze",
            "listening" => "typing",
            "spellingbee" => "flashcard",
            "flashcard" => "typing",
            _ => "flashcard"
        };

        private static string GetWordDeclension(int count)
        {
            var mod100 = count % 100;
            var mod10 = count % 10;
            if (mod100 >= 11 && mod100 <= 19) return "слов";
            if (mod10 == 1) return "слово";
            if (mod10 >= 2 && mod10 <= 4) return "слова";
            return "слов";
        }

        // ── §1.1 Context-Aware Hints ─────────────────────────────────

        private string? GetCurrentWordUserNote()
        {
            if (CurrentWord == null) return null;
            // TrainingWordDto path — notes stored in dictionary
            if (_userNotes.TryGetValue(CurrentWord.Id, out var note) && !string.IsNullOrWhiteSpace(note))
                return note;
            // Dictionary path — notes stored in LearningProgress navigation property
            return CurrentWord.Progress?.FirstOrDefault()?.UserNote;
        }

        private void UseHint()
        {
            if (CurrentWord == null) return;

            _hintCount++;
            HintUsed = true;
            var answer = ExpectedAnswer;

            // Context-aware hint ladder:
            // Level 1: Example sentence (context)
            // Level 2: First letter + letter count (partial retrieval cue)
            // Level 3: User note / mnemonic (personal association)
            // Level 4: Half-word reveal (last resort)
            HintText = _hintCount switch
            {
                1 when !string.IsNullOrEmpty(CurrentWord.Example)
                    => $"📖 {CurrentWord.Example}",
                1 => $"Первая буква: {answer[0]}… ({answer.Length} букв)",

                2 => $"💡 {answer[0]}{'…'} — {answer.Length} букв ({new string('●', answer.Length)})",

                3 when !string.IsNullOrWhiteSpace(GetCurrentWordUserNote())
                    => $"📝 {GetCurrentWordUserNote()}",
                3 => $"🔤 {answer[..Math.Min(answer.Length, answer.Length / 2 + 1)]}…",

                4 => $"🔤 {answer[..Math.Min(answer.Length, answer.Length / 2 + 1)]}…",

                _ => HintText
            };

            (UseHintCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        private void ResetHint()
        {
            _hintCount = 0;
            HintText = "";
            HintUsed = false;
        }

        // ── #16 Typing Diff ──────────────────────────────────────────

        private void BuildTypingDiff(string userAnswer, string correctAnswer)
        {
            // When strings are too different, skip per-character diff
            int maxLen = Math.Max(userAnswer.Length, correctAnswer.Length);
            if (maxLen == 0)
            {
                TypingDiffCorrect = correctAnswer;
                TypingDiffUser = userAnswer;
                return;
            }

            // Compute LCS to align characters properly
            var lcs = ComputeLcs(userAnswer.ToLowerInvariant(), correctAnswer.ToLowerInvariant());

            // If similarity is very low, just show plain strings
            double similarity = (double)lcs.Count / maxLen;
            if (similarity < 0.3)
            {
                TypingDiffUser = userAnswer;
                TypingDiffCorrect = correctAnswer;
                return;
            }

            // Build diff using LCS alignment
            var diffUser = new System.Text.StringBuilder();
            var diffCorrect = new System.Text.StringBuilder();
            var lcsSet = new HashSet<(int, int)>(lcs);

            int ui = 0, ci = 0, li = 0;
            while (ui < userAnswer.Length || ci < correctAnswer.Length)
            {
                if (li < lcs.Count && ui == lcs[li].Item1 && ci == lcs[li].Item2)
                {
                    // Matched character
                    diffUser.Append(userAnswer[ui]);
                    diffCorrect.Append(correctAnswer[ci]);
                    ui++; ci++; li++;
                }
                else
                {
                    // Check if the current user char is part of a future LCS match
                    bool userInLcs = li < lcs.Count && ui < userAnswer.Length && ui < lcs[li].Item1;
                    bool correctInLcs = li < lcs.Count && ci < correctAnswer.Length && ci < lcs[li].Item2;

                    if (userInLcs && !correctInLcs)
                    {
                        diffUser.Append($"[{userAnswer[ui]}]");
                        ui++;
                    }
                    else if (correctInLcs && !userInLcs)
                    {
                        diffCorrect.Append($"[{correctAnswer[ci]}]");
                        ci++;
                    }
                    else
                    {
                        if (ui < userAnswer.Length)
                        {
                            diffUser.Append($"[{userAnswer[ui]}]");
                            ui++;
                        }
                        if (ci < correctAnswer.Length)
                        {
                            diffCorrect.Append($"[{correctAnswer[ci]}]");
                            ci++;
                        }
                    }
                }
            }

            TypingDiffUser = diffUser.ToString();
            TypingDiffCorrect = diffCorrect.ToString();
        }

        private static List<(int, int)> ComputeLcs(string a, string b)
        {
            int m = a.Length, n = b.Length;
            var dp = new int[m + 1, n + 1];

            for (int i = 1; i <= m; i++)
                for (int j = 1; j <= n; j++)
                    dp[i, j] = a[i - 1] == b[j - 1]
                        ? dp[i - 1, j - 1] + 1
                        : Math.Max(dp[i - 1, j], dp[i, j - 1]);

            // Backtrack to find matched indices
            var result = new List<(int, int)>();
            int x = m, y = n;
            while (x > 0 && y > 0)
            {
                if (a[x - 1] == b[y - 1])
                {
                    result.Add((x - 1, y - 1));
                    x--; y--;
                }
                else if (dp[x - 1, y] >= dp[x, y - 1])
                    x--;
                else
                    y--;
            }

            result.Reverse();
            return result;
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
            if (_allWords != null && _allWords.Count >= 4)
                types.Add("matching");
            if (_allWords != null && _allWords.Any(w => !string.IsNullOrWhiteSpace(w.Example)))
                types.Add("cloze");
            types.Add("listening");
            types.Add("spellingbee");
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

        // ── §18.1d Speed Round ─────────────────────────────────────────

        private void StartSpeedRound()
        {
            if (_allWords == null || _allWords.Count < 4) return;

            IsSpeedRound = true;
            SpeedRoundScore = 0;
            SpeedRoundTimeLeft = 60;

            // Reset the queue with all words for the speed round
            var shuffled = _allWords.OrderBy(_ => _random.Next()).ToList();
            _wordsQueue = new Queue<Word>(shuffled);
            _totalWordsCount = shuffled.Count;
            CurrentWord = _wordsQueue.Peek();
            IsFlipped = false;

            // Force MCQ mode for speed
            _isMixedMode = false;
            ExerciseType = "mcq";
            IsExerciseTypeChosen = true;
            PrepareCurrentExercise();

            _speedTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _speedTimer.Tick += SpeedTimer_Tick;
            _speedTimer.Start();
        }

        private void SpeedTimer_Tick(object sender, EventArgs e)
        {
            SpeedRoundTimeLeft--;
            if (SpeedRoundTimeLeft <= 0)
            {
                _speedTimer.Stop();
                _speedTimer.Tick -= SpeedTimer_Tick;
                _ = CompleteSessionAsync();
            }
        }

        private void AddSpeedScore(bool correct)
        {
            if (!IsSpeedRound) return;
            if (correct)
                SpeedRoundScore += Math.Max(1, SpeedRoundTimeLeft / 10 + 1);
        }

        // ── §18.1c Spelling Bee ─────────────────────────────────────────

        private void PrepareSpellingBee()
        {
            if (CurrentWord == null) return;

            // Play the word audio
            _speechService.Speak(CurrentWord.OriginalWord, _languageFrom);

            // Generate hint: show first letter + word length
            var word = CurrentWord.OriginalWord;
            SpellingHint = $"Первая буква: {word[0].ToString().ToUpper()}, {word.Length} букв";
        }

        private async Task HandleCheckSpellingAsync()
        {
            if (_isProcessing || SpellingAnswered || CurrentWord == null) return;
            _isProcessing = true;
            try
            {
                SpellingAnswered = true;

                var userAnswer = SpellingAnswer.Trim();
                var correct = CurrentWord.OriginalWord.Trim();

                SpellingWasCorrect = string.Equals(userAnswer, correct, StringComparison.OrdinalIgnoreCase);

                var quality = SpellingWasCorrect
                    ? (HintUsed ? ResponseQuality.Hard : ResponseQuality.Good)
                    : ResponseQuality.Again;
                await TrackAndSubmitAsync(quality);

                if (IsSpeedRound)
                {
                    AddSpeedScore(SpellingWasCorrect);
                    await Task.Delay(400);
                    _isProcessing = false;
                    await MoveToNextWordAsync();
                    return;
                }

                (CheckSpellingCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (NextSpellingWordCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
            finally
            {
                _isProcessing = false;
            }
        }

        // ── §18.1b Cloze Mode ─────────────────────────────────────────

        private void GenerateClozeSentence()
        {
            if (CurrentWord == null) return;

            var word = CurrentWord;
            var example = word.Example;
            var target = word.OriginalWord;

            if (!string.IsNullOrWhiteSpace(example) && example.Contains(target, StringComparison.OrdinalIgnoreCase))
            {
                // Replace the word in the example with a blank
                var idx = example.IndexOf(target, StringComparison.OrdinalIgnoreCase);
                var blank = new string('_', Math.Max(target.Length, 4));
                ClozeSentence = example.Substring(0, idx) + blank + example.Substring(idx + target.Length);
                ClozeCorrectWord = target;
            }
            else if (!string.IsNullOrWhiteSpace(example))
            {
                // Example doesn't contain the word — show example with blank for the word
                ClozeSentence = $"«{example}»\n\nНапишите слово: {new string('_', Math.Max(target.Length, 4))}";
                ClozeCorrectWord = target;
            }
            else
            {
                // No example — generate a simple cloze from translation
                ClozeSentence = $"Переведите: {word.Translation}\n\n{new string('_', Math.Max(target.Length, 4))}";
                ClozeCorrectWord = target;
            }
        }

        private async Task HandleCheckClozeAsync()
        {
            if (_isProcessing || ClozeAnswered || CurrentWord == null) return;
            _isProcessing = true;
            try
            {
                ClozeAnswered = true;

                var userAnswer = ClozeAnswer.Trim();
                var correct = ClozeCorrectWord.Trim();

                ClozeWasCorrect = string.Equals(userAnswer, correct, StringComparison.OrdinalIgnoreCase);

                var quality = ClozeWasCorrect
                    ? (HintUsed ? ResponseQuality.Hard : ResponseQuality.Good)
                    : ResponseQuality.Again;
                await TrackAndSubmitAsync(quality);

                if (IsSpeedRound)
                {
                    AddSpeedScore(ClozeWasCorrect);
                    await Task.Delay(400);
                    _isProcessing = false;
                    await MoveToNextWordAsync();
                    return;
                }

                (CheckClozeCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (NextClozeWordCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
            finally
            {
                _isProcessing = false;
            }
        }

        // ── §18.1a Matching Mode ────────────────────────────────────────

        private void GenerateMatchingBoard()
        {
            MatchItems.Clear();
            MatchingCompleted = false;
            MatchedPairsCount = 0;
            _selectedMatchItem = null;
            _isMatchAnimating = false;

            // Pick up to 5 random words for matching
            var words = _allWords.OrderBy(_ => _random.Next()).Take(5).ToList();
            MatchTotalPairs = words.Count;

            var originals = words.Select(w => new MatchItem
            {
                WordId = w.Id,
                Text = IsReversed ? w.Translation : w.OriginalWord,
                IsOriginal = true
            }).ToList();

            var translations = words.Select(w => new MatchItem
            {
                WordId = w.Id,
                Text = IsReversed ? w.OriginalWord : w.Translation,
                IsOriginal = false
            }).ToList();

            // Shuffle each column independently
            var shuffledOriginals = originals.OrderBy(_ => _random.Next()).ToList();
            var shuffledTranslations = translations.OrderBy(_ => _random.Next()).ToList();

            // Interleave: originals first, then translations (UI will display in 2 columns)
            foreach (var item in shuffledOriginals)
                MatchItems.Add(item);
            foreach (var item in shuffledTranslations)
                MatchItems.Add(item);
        }

        private async void HandleMatchSelection(MatchItem item)
        {
            if (item.IsMatched || _isMatchAnimating) return;

            if (_selectedMatchItem == null)
            {
                // First selection
                _selectedMatchItem = item;
                item.IsSelected = true;
                return;
            }

            // Second selection
            if (_selectedMatchItem == item)
            {
                // Deselect
                item.IsSelected = false;
                _selectedMatchItem = null;
                return;
            }

            // Must select one from each side
            if (_selectedMatchItem.IsOriginal == item.IsOriginal)
            {
                _selectedMatchItem.IsSelected = false;
                _selectedMatchItem = item;
                item.IsSelected = true;
                return;
            }

            // Check match
            if (_selectedMatchItem.WordId == item.WordId)
            {
                // Correct match!
                _selectedMatchItem.IsMatched = true;
                _selectedMatchItem.IsSelected = false;
                _selectedMatchItem = null;

                item.IsMatched = true;
                item.IsSelected = false;
                MatchedPairsCount++;
                CorrectCount++;
                CurrentStreak++;

                if (IsSpeedRound) AddSpeedScore(true);

                // Submit progress for the matched word
                try
                {
                    var word = _allWords.FirstOrDefault(w => w.Id == item.WordId);
                    if (word != null)
                    {
                        await _dataService.UpdateProgressAsync(new UpdateProgressRequest
                        {
                            WordId = word.Id,
                            Quality = ResponseQuality.Good
                        });
                    }
                }
                catch { }

                if (MatchedPairsCount >= MatchTotalPairs)
                {
                    MatchingCompleted = true;
                    (NextMatchingRoundCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
            else
            {
                // Wrong match — capture the selected item before the await
                // because rapid clicks can nullify _selectedMatchItem during the delay
                var previousSelection = _selectedMatchItem;
                _selectedMatchItem = null;
                _isMatchAnimating = true;

                item.IsWrong = true;
                previousSelection.IsWrong = true;
                WrongCount++;
                CurrentStreak = 0;

                if (IsSpeedRound) AddSpeedScore(false);

                await Task.Delay(500);
                item.IsWrong = false;
                item.IsSelected = false;
                previousSelection.IsWrong = false;
                previousSelection.IsSelected = false;
                _isMatchAnimating = false;
            }

            UpdateProgress();
        }

        public override void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_speedTimer != null)
            {
                _speedTimer.Stop();
                _speedTimer.Tick -= SpeedTimer_Tick;
            }
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
