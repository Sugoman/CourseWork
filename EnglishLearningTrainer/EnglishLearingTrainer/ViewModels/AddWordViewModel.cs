using EnglishLearningTrainer.Core;
using EnglishLearningTrainer.Models;
using EnglishLearningTrainer.Services;
using System.Windows.Input;

namespace EnglishLearningTrainer.ViewModels
{
    public class AddWordViewModel : TabViewModelBase
    {
        private readonly IDataService _dataService;
        private readonly Dictionary _selectedDictionary;
        private readonly SpellCheckService _spellCheckService;

        public string Translation { get; set; }
        public string Example { get; set; }
        private string _suggestion;
        public string Suggestion
        {
            get => _suggestion;
            set => SetProperty(ref _suggestion, value);
        }

        private string _originalWord;
        public string OriginalWord
        {
            get => _originalWord;
            set
            {
                SetProperty(ref _originalWord, value);
                UpdateSuggestion();
            }
        }

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }

        public AddWordViewModel(IDataService dataService, Dictionary dictionary)
        {
            _dataService = dataService;
            _selectedDictionary = dictionary;
            _spellCheckService = new SpellCheckService();
            Title = $"Добавить слово в {dictionary.Name}";

            SaveCommand = new RelayCommand(async (param) => await SaveWordAsync());
            CancelCommand = new RelayCommand((param) => Cancel());
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
        private async Task SaveWordAsync()
        {
            System.Diagnostics.Debug.WriteLine("=== SAVE WORD STARTED ===");

            if (string.IsNullOrWhiteSpace(OriginalWord) || string.IsNullOrWhiteSpace(Translation))
            {
                System.Diagnostics.Debug.WriteLine("Ошибка: не заполнены обязательные поля");
                return;
            }

            try
            {
                var newWord = new Word
                {
                    DictionaryId = _selectedDictionary.Id,
                    OriginalWord = OriginalWord.Trim(),
                    Translation = Translation.Trim(),
                    Example = Example?.Trim() ?? "",
                    AddedAt = DateTime.Now
                };

                System.Diagnostics.Debug.WriteLine($"Добавляем слово: {newWord.OriginalWord}");

                await _dataService.AddWordAsync(newWord);
                System.Diagnostics.Debug.WriteLine($"Слово '{OriginalWord}' успешно добавлено в БД!");

                EventAggregator.Instance.Publish(new RefreshDataMessage());
                EventAggregator.Instance.Publish(this);

                System.Diagnostics.Debug.WriteLine("=== SAVE WORD COMPLETED ===");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка при сохранении: {ex.Message}");
            }
        }

        private void Cancel()
        {
            System.Diagnostics.Debug.WriteLine("Добавление слова отменено");
            EventAggregator.Instance.Publish(this);
        }

        private void UpdateSuggestion()
        {
            Suggestion = _spellCheckService.SuggestCorrection(OriginalWord);
        }
    }
}