using LearningTrainer.Core;
using LearningTrainer.Services;
using LearningTrainerShared.Models;
using System.Collections.ObjectModel;
using System.Threading;
using System.Windows.Input;

namespace LearningTrainer.ViewModels
{
    public class AddWordViewModel : TabViewModelBase
    {
        private readonly IDataService _dataService;
        private readonly Dictionary _selectedDictionary;
        private readonly SpellCheckService _spellCheckService;
        private readonly TranslationService _translationService;
        private readonly ExternalDictionaryService _externalDictionaryService;
        private readonly ObservableCollection<Word> _existingWords;

        /// <summary>Слово, которое редактируется (null = режим создания).</summary>
        private readonly Word? _editingWord;
        public bool IsEditMode => _editingWord != null;

        /// <summary>Debounce для асинхронного спеллчека.</summary>
        private CancellationTokenSource _spellCheckCts;
        private static readonly TimeSpan SpellCheckDebounce = TimeSpan.FromMilliseconds(300);

        private string _translation;
        public string Translation
        {
            get => _translation;
            set => SetProperty(ref _translation, value);
        }

        private string _example;
        public string Example
        {
            get => _example;
            set => SetProperty(ref _example, value);
        }

        private string _suggestion;
        public string Suggestion
        {
            get => _suggestion;
            set => SetProperty(ref _suggestion, value);
        }

        private string _duplicateWarning;
        public string DuplicateWarning
        {
            get => _duplicateWarning;
            set => SetProperty(ref _duplicateWarning, value);
        }

        private int _addedCount;
        public int AddedCount
        {
            get => _addedCount;
            set => SetProperty(ref _addedCount, value);
        }

        private string _originalWord;
        public string OriginalWord
        {
            get => _originalWord;
            set
            {
                if (SetProperty(ref _originalWord, value))
                {
                    UpdateSuggestion();
                    CheckDuplicate();
                }
            }
        }

        // Подсказки из API
        private string _suggestedExample;
        public string SuggestedExample
        {
            get => _suggestedExample;
            set => SetProperty(ref _suggestedExample, value);
        }

        private bool _isAutoTranslating;
        public bool IsAutoTranslating
        {
            get => _isAutoTranslating;
            set => SetProperty(ref _isAutoTranslating, value);
        }

        private bool _isFetchingDetails;
        public bool IsFetchingDetails
        {
            get => _isFetchingDetails;
            set => SetProperty(ref _isFetchingDetails, value);
        }

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand DoneCommand { get; }
        public ICommand AutoTranslateCommand { get; }
        public ICommand FetchExampleCommand { get; }
        public ICommand AcceptExampleCommand { get; }

        /// <summary>
        /// Событие, вызываемое после успешного сохранения слова для установки фокуса на поле OriginalWord.
        /// </summary>
        public event Action WordSaved;

        /// <summary>Конструктор для добавления нового слова.</summary>
        public AddWordViewModel(IDataService dataService, Dictionary dictionary, ObservableCollection<Word> existingWords)
            : this(dataService, dictionary, existingWords, null)
        {
        }

        /// <summary>Конструктор для редактирования существующего слова.</summary>
        public AddWordViewModel(IDataService dataService, Dictionary dictionary, ObservableCollection<Word> existingWords, Word? editingWord)
        {
            _dataService = dataService;
            _selectedDictionary = dictionary;
            _existingWords = existingWords;
            _editingWord = editingWord;
            _spellCheckService = new SpellCheckService(dictionary.LanguageFrom ?? "English");
            _translationService = new TranslationService();
            _externalDictionaryService = new ExternalDictionaryService(new System.Net.Http.HttpClient());

            if (IsEditMode)
            {
                SetLocalizedTitle("Loc.Tab.EditWord", $": {dictionary.Name}");
                _originalWord = editingWord!.OriginalWord;
                _translation = editingWord.Translation;
                _example = editingWord.Example;
            }
            else
            {
                SetLocalizedTitle("Loc.Tab.AddWord", $": {dictionary.Name}");
            }

            SaveCommand = new RelayCommand(async (param) => await SaveWordAsync());
            CancelCommand = new RelayCommand((param) => Cancel());
            DoneCommand = new RelayCommand((param) =>
                EventAggregator.Instance.Publish(new EventAggregator.CloseTabMessage(this)));
            AutoTranslateCommand = new RelayCommand(async (param) => await AutoTranslateAsync());
            FetchExampleCommand = new RelayCommand(async (param) => await FetchExampleAsync());
            AcceptExampleCommand = new RelayCommand((param) =>
            {
                if (!string.IsNullOrEmpty(SuggestedExample))
                {
                    Example = SuggestedExample;
                    SuggestedExample = null;
                }
            });
        }

        public bool AcceptSuggestion()
        {
            if (!string.IsNullOrWhiteSpace(Suggestion))
            {
                OriginalWord = Suggestion;
                Suggestion = null;
                return true;
            }
            return false;
        }

        private async Task AutoTranslateAsync()
        {
            if (string.IsNullOrWhiteSpace(OriginalWord))
                return;

            IsAutoTranslating = true;
            try
            {
                var result = await _translationService.TranslateAsync(
                    OriginalWord.Trim(),
                    _selectedDictionary.LanguageFrom ?? "English",
                    _selectedDictionary.LanguageTo ?? "Russian");

                if (!string.IsNullOrEmpty(result))
                    Translation = result;
                else
                    EventAggregator.Instance.Publish(ShowNotificationMessage.Info(
                        "Автоперевод", "Не удалось получить перевод"));
            }
            catch (Exception ex)
            {
                EventAggregator.Instance.Publish(ShowNotificationMessage.Error(
                    "Автоперевод", $"Ошибка: {ex.Message}"));
            }
            finally
            {
                IsAutoTranslating = false;
            }
        }

        private async Task FetchExampleAsync()
        {
            if (string.IsNullOrWhiteSpace(OriginalWord))
                return;

            IsFetchingDetails = true;
            try
            {
                var details = await _externalDictionaryService.GetWordDetailsAsync(OriginalWord.Trim());
                if (details?.Example != null)
                {
                    SuggestedExample = details.Example;
                }
                else
                {
                    EventAggregator.Instance.Publish(ShowNotificationMessage.Info(
                        "Пример", "Пример не найден в словаре"));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"FetchExample error: {ex.Message}");
            }
            finally
            {
                IsFetchingDetails = false;
            }
        }

        private async Task SaveWordAsync()
        {
            if (string.IsNullOrWhiteSpace(OriginalWord) || string.IsNullOrWhiteSpace(Translation))
            {
                return;
            }

            // Блокируем сохранение при наличии дубля (кроме редактирования того же слова)
            if (!string.IsNullOrEmpty(DuplicateWarning))
            {
                EventAggregator.Instance.Publish(ShowNotificationMessage.Info(
                    "Дубликат", DuplicateWarning));
                return;
            }

            try
            {
                if (IsEditMode)
                {
                    _editingWord!.OriginalWord = OriginalWord.Trim();
                    _editingWord.Translation = Translation.Trim();
                    _editingWord.Example = Example?.Trim() ?? "";

                    var success = await _dataService.UpdateWordAsync(_editingWord);
                    if (success)
                    {
                        EventAggregator.Instance.Publish(ShowNotificationMessage.Success(
                            "Сохранено", $"Слово «{_editingWord.OriginalWord}» обновлено"));
                        EventAggregator.Instance.Publish(new EventAggregator.CloseTabMessage(this));
                    }
                    else
                    {
                        EventAggregator.Instance.Publish(ShowNotificationMessage.Error(
                            "Ошибка", "Не удалось обновить слово"));
                    }
                }
                else
                {
                    var newWord = new Word
                    {
                        DictionaryId = _selectedDictionary.Id,
                        OriginalWord = OriginalWord.Trim(),
                        Translation = Translation.Trim(),
                        Example = Example?.Trim() ?? "",
                        AddedAt = DateTime.UtcNow
                    };

                    var savedWord = await _dataService.AddWordAsync(newWord);

                    EventAggregator.Instance.Publish(new WordAddedMessage(savedWord, savedWord.DictionaryId));

                    AddedCount++;

                    // Очищаем поля для следующего слова
                    OriginalWord = string.Empty;
                    Translation = string.Empty;
                    Example = string.Empty;
                    Suggestion = null;
                    DuplicateWarning = null;
                    SuggestedExample = null;

                    WordSaved?.Invoke();
                }
            }
            catch (Exception ex)
            {
                EventAggregator.Instance.Publish(ShowNotificationMessage.Error(
                    "Ошибка сохранения", $"Не удалось сохранить слово: {ex.Message}"));
            }
        }

        private void Cancel()
        {
            EventAggregator.Instance.Publish(new EventAggregator.CloseTabMessage(this));
        }

        private void UpdateSuggestion()
        {
            // Отменяем предыдущий запрос
            _spellCheckCts?.Cancel();
            _spellCheckCts = new CancellationTokenSource();
            var ct = _spellCheckCts.Token;
            var word = OriginalWord;

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(SpellCheckDebounce, ct);
                    ct.ThrowIfCancellationRequested();

                    var result = _spellCheckService.SuggestCorrection(word);

                    ct.ThrowIfCancellationRequested();

                    // Обновляем UI из потока диспетчера
                    System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                    {
                        if (!ct.IsCancellationRequested)
                            Suggestion = result;
                    });
                }
                catch (OperationCanceledException) { }
            }, ct);
        }

        private void CheckDuplicate()
        {
            if (string.IsNullOrWhiteSpace(OriginalWord) || _existingWords == null)
            {
                DuplicateWarning = null;
                return;
            }

            var trimmed = OriginalWord.Trim();
            var duplicate = _existingWords.FirstOrDefault(w =>
                string.Equals(w.OriginalWord, trimmed, StringComparison.OrdinalIgnoreCase)
                && (_editingWord == null || w.Id != _editingWord.Id));

            DuplicateWarning = duplicate != null
                ? $"Слово «{duplicate.OriginalWord}» уже есть в словаре (перевод: {duplicate.Translation})"
                : null;
        }
    }
}
