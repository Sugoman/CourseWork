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

        public string OriginalWord { get; set; }
        public string Translation { get; set; }
        public string Example { get; set; }

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }

        public AddWordViewModel(IDataService dataService, Dictionary dictionary)
        {
            _dataService = dataService;
            _selectedDictionary = dictionary;
            Title = $"Добавить слово в {dictionary.Name}";

            SaveCommand = new RelayCommand(async (param) => await SaveWordAsync());
            CancelCommand = new RelayCommand((param) => Cancel());
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
    }
}