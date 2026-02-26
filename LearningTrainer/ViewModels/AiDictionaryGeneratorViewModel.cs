using LearningTrainer.Core;
using LearningTrainer.Services;
using LearningTrainerShared.Models;
using LearningTrainerShared.Models.Features.Ai;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace LearningTrainer.ViewModels
{
    public class AiDictionaryGeneratorViewModel : TabViewModelBase
    {
        private readonly IDataService _dataService;
        private readonly IAiTranslationService _aiService;

        private string _topic = "";
        public string Topic
        {
            get => _topic;
            set => SetProperty(ref _topic, value);
        }

        private string _dictionaryName = "";
        public string DictionaryName
        {
            get => _dictionaryName;
            set => SetProperty(ref _dictionaryName, value);
        }

        private string _selectedLanguageFrom = "English";
        public string SelectedLanguageFrom
        {
            get => _selectedLanguageFrom;
            set => SetProperty(ref _selectedLanguageFrom, value);
        }

        private string _selectedLanguageTo = "Russian";
        public string SelectedLanguageTo
        {
            get => _selectedLanguageTo;
            set => SetProperty(ref _selectedLanguageTo, value);
        }

        private string _selectedLevel = "A2";
        public string SelectedLevel
        {
            get => _selectedLevel;
            set => SetProperty(ref _selectedLevel, value);
        }

        private int _wordCount = 10;
        public int WordCount
        {
            get => _wordCount;
            set => SetProperty(ref _wordCount, Math.Clamp(value, 5, 30));
        }

        private bool _isGenerating;
        public bool IsGenerating
        {
            get => _isGenerating;
            set => SetProperty(ref _isGenerating, value);
        }

        private bool _isSaving;
        public bool IsSaving
        {
            get => _isSaving;
            set => SetProperty(ref _isSaving, value);
        }

        private bool _hasResults;
        public bool HasResults
        {
            get => _hasResults;
            set => SetProperty(ref _hasResults, value);
        }

        private string _statusMessage;
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public ObservableCollection<AiGeneratedWordEntry> GeneratedWords { get; } = new();

        public List<string> Languages { get; } = new() { "English", "Russian", "German", "Spanish", "French", "Italian", "Portuguese", "Chinese", "Japanese", "Korean" };
        public List<string> LanguageLevels { get; } = new() { "A1", "A2", "B1", "B2", "C1", "C2" };

        public ICommand GenerateCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand RemoveWordCommand { get; }
        public ICommand ClearCommand { get; }
        public ICommand DoneCommand { get; }

        public AiDictionaryGeneratorViewModel(IDataService dataService)
        {
            _dataService = dataService;
            _aiService = CreateAiService();

            SetLocalizedTitle("Loc.Tab.AiGenerator");

            GenerateCommand = new RelayCommand(async (p) => await GenerateAsync());
            SaveCommand = new RelayCommand(async (p) => await SaveAsync());
            RemoveWordCommand = new RelayCommand((p) =>
            {
                if (p is AiGeneratedWordEntry entry)
                    GeneratedWords.Remove(entry);
            });
            ClearCommand = new RelayCommand((p) =>
            {
                GeneratedWords.Clear();
                HasResults = false;
                StatusMessage = null;
            });
            DoneCommand = new RelayCommand((p) =>
                EventAggregator.Instance.Publish(new EventAggregator.CloseTabMessage(this)));
        }

        private async Task GenerateAsync()
        {
            if (string.IsNullOrWhiteSpace(Topic))
            {
                EventAggregator.Instance.Publish(ShowNotificationMessage.Info(
                    "ИИ-генератор", "Укажите тему для генерации."));
                return;
            }

            IsGenerating = true;
            StatusMessage = "Генерация слов...";
            GeneratedWords.Clear();
            HasResults = false;

            try
            {
                var words = await _aiService.GenerateDictionaryAsync(
                    Topic.Trim(),
                    SelectedLanguageFrom,
                    SelectedLanguageTo,
                    SelectedLevel,
                    WordCount);

                if (words.Count > 0)
                {
                    foreach (var w in words)
                        GeneratedWords.Add(w);

                    HasResults = true;
                    StatusMessage = $"Сгенерировано {words.Count} слов";

                    if (string.IsNullOrWhiteSpace(DictionaryName))
                        DictionaryName = Topic.Trim();
                }
                else
                {
                    StatusMessage = "ИИ не смог сгенерировать слова. Попробуйте другую тему.";
                    EventAggregator.Instance.Publish(ShowNotificationMessage.Error(
                        "ИИ-генератор", StatusMessage));
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Ошибка: {ex.Message}";
                EventAggregator.Instance.Publish(ShowNotificationMessage.Error(
                    "ИИ-генератор", $"Не удалось сгенерировать: {ex.Message}"));
            }
            finally
            {
                IsGenerating = false;
            }
        }

        private async Task SaveAsync()
        {
            if (GeneratedWords.Count == 0)
                return;

            if (string.IsNullOrWhiteSpace(DictionaryName))
            {
                EventAggregator.Instance.Publish(ShowNotificationMessage.Info(
                    "ИИ-генератор", "Укажите название словаря."));
                return;
            }

            IsSaving = true;
            try
            {
                // 1. Создаём словарь
                var dictionary = new Dictionary
                {
                    Name = DictionaryName.Trim(),
                    Description = $"Сгенерировано ИИ по теме: {Topic.Trim()}",
                    LanguageFrom = SelectedLanguageFrom,
                    LanguageTo = SelectedLanguageTo
                };
                var savedDict = await _dataService.AddDictionaryAsync(dictionary);

                // 2. Добавляем слова
                var words = GeneratedWords.Select(w => new Word
                {
                    DictionaryId = savedDict.Id,
                    OriginalWord = w.Original,
                    Translation = w.Translation,
                    Example = w.Example ?? "",
                    AddedAt = DateTime.UtcNow
                }).ToList();

                var (added, skipped, savedWords) = await _dataService.AddWordsBatchAsync(words);

                EventAggregator.Instance.Publish(ShowNotificationMessage.Success(
                    "ИИ-генератор", $"Словарь «{savedDict.Name}» создан: {added} слов добавлено."));

                // Уведомляем DashboardViewModel о новом словаре
                EventAggregator.Instance.Publish(new DictionaryAddedMessage(savedDict));

                EventAggregator.Instance.Publish(new EventAggregator.CloseTabMessage(this));
            }
            catch (Exception ex)
            {
                EventAggregator.Instance.Publish(ShowNotificationMessage.Error(
                    "Ошибка сохранения", $"Не удалось сохранить словарь: {ex.Message}"));
            }
            finally
            {
                IsSaving = false;
            }
        }

        private static IAiTranslationService CreateAiService() => AiServiceFactory.Create();
    }
}
