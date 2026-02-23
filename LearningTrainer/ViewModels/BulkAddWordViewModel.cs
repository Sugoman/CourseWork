using LearningTrainer.Core;
using LearningTrainer.Services;
using LearningTrainerShared.Models;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace LearningTrainer.ViewModels
{
    public class BulkWordEntry : ObservableObject
    {
        private string _originalWord = "";
        public string OriginalWord
        {
            get => _originalWord;
            set => SetProperty(ref _originalWord, value);
        }

        private string _translation = "";
        public string Translation
        {
            get => _translation;
            set => SetProperty(ref _translation, value);
        }

        private string _example = "";
        public string Example
        {
            get => _example;
            set => SetProperty(ref _example, value);
        }

        private bool _isDuplicate;
        public bool IsDuplicate
        {
            get => _isDuplicate;
            set => SetProperty(ref _isDuplicate, value);
        }
    }

    public class BulkAddWordViewModel : TabViewModelBase
    {
        private readonly IDataService _dataService;
        private readonly Dictionary _selectedDictionary;
        private readonly ObservableCollection<Word> _existingWords;

        private string _rawText = "";
        public string RawText
        {
            get => _rawText;
            set => SetProperty(ref _rawText, value);
        }

        public ObservableCollection<BulkWordEntry> ParsedWords { get; } = new();

        private bool _isParsed;
        public bool IsParsed
        {
            get => _isParsed;
            set => SetProperty(ref _isParsed, value);
        }

        private bool _isSaving;
        public bool IsSaving
        {
            get => _isSaving;
            set => SetProperty(ref _isSaving, value);
        }

        private string _resultMessage;
        public string ResultMessage
        {
            get => _resultMessage;
            set => SetProperty(ref _resultMessage, value);
        }

        public int ValidCount => ParsedWords.Count(w => !w.IsDuplicate
            && !string.IsNullOrWhiteSpace(w.OriginalWord)
            && !string.IsNullOrWhiteSpace(w.Translation));

        public ICommand ParseCommand { get; }
        public ICommand SaveAllCommand { get; }
        public ICommand RemoveEntryCommand { get; }
        public ICommand BackToInputCommand { get; }
        public ICommand DoneCommand { get; }

        public BulkAddWordViewModel(IDataService dataService, Dictionary dictionary, ObservableCollection<Word> existingWords)
        {
            _dataService = dataService;
            _selectedDictionary = dictionary;
            _existingWords = existingWords;

            SetLocalizedTitle("Loc.Tab.BulkAdd", $": {dictionary.Name}");

            ParseCommand = new RelayCommand((p) => ParseText());
            SaveAllCommand = new RelayCommand(async (p) => await SaveAllAsync());
            RemoveEntryCommand = new RelayCommand((p) =>
            {
                if (p is BulkWordEntry entry)
                {
                    ParsedWords.Remove(entry);
                    OnPropertyChanged(nameof(ValidCount));
                }
            });
            BackToInputCommand = new RelayCommand((p) =>
            {
                IsParsed = false;
                ParsedWords.Clear();
                ResultMessage = null;
            });
            DoneCommand = new RelayCommand((p) =>
                EventAggregator.Instance.Publish(new EventAggregator.CloseTabMessage(this)));
        }

        private void ParseText()
        {
            ParsedWords.Clear();
            ResultMessage = null;

            if (string.IsNullOrWhiteSpace(RawText))
                return;

            var lines = RawText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var existingSet = new HashSet<string>(
                _existingWords.Select(w => w.OriginalWord.ToLower()));

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                // Попытка разделить по разделителям: - — = Tab
                string? word = null, translation = null;

                foreach (var separator in new[] { "\t", " - ", " — ", " = ", " – " })
                {
                    var idx = trimmed.IndexOf(separator, StringComparison.Ordinal);
                    if (idx > 0)
                    {
                        word = trimmed.Substring(0, idx).Trim();
                        translation = trimmed.Substring(idx + separator.Length).Trim();
                        break;
                    }
                }

                if (word == null || translation == null)
                    continue;

                var entry = new BulkWordEntry
                {
                    OriginalWord = word,
                    Translation = translation,
                    IsDuplicate = existingSet.Contains(word.ToLower())
                };

                ParsedWords.Add(entry);
            }

            IsParsed = true;
            OnPropertyChanged(nameof(ValidCount));
        }

        private async Task SaveAllAsync()
        {
            var validEntries = ParsedWords
                .Where(w => !w.IsDuplicate
                    && !string.IsNullOrWhiteSpace(w.OriginalWord)
                    && !string.IsNullOrWhiteSpace(w.Translation))
                .ToList();

            if (validEntries.Count == 0)
            {
                EventAggregator.Instance.Publish(ShowNotificationMessage.Info(
                    "Нечего сохранять", "Все строки пустые или являются дубликатами."));
                return;
            }

            IsSaving = true;
            try
            {
                var words = validEntries.Select(e => new Word
                {
                    DictionaryId = _selectedDictionary.Id,
                    OriginalWord = e.OriginalWord.Trim(),
                    Translation = e.Translation.Trim(),
                    Example = e.Example?.Trim() ?? "",
                    AddedAt = DateTime.UtcNow
                }).ToList();

                var (added, skipped, savedWords) = await _dataService.AddWordsBatchAsync(words);

                foreach (var w in savedWords)
                {
                    _existingWords.Add(w);
                    EventAggregator.Instance.Publish(new WordAddedMessage(w, w.DictionaryId));
                }

                ResultMessage = $"✓ Добавлено: {added}, пропущено (дубли): {skipped}";
                EventAggregator.Instance.Publish(ShowNotificationMessage.Success(
                    "Массовое добавление", ResultMessage));
            }
            catch (Exception ex)
            {
                EventAggregator.Instance.Publish(ShowNotificationMessage.Error(
                    "Ошибка", $"Не удалось сохранить: {ex.Message}"));
            }
            finally
            {
                IsSaving = false;
            }
        }
    }
}
