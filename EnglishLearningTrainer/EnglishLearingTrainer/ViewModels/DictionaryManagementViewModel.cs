using EnglishLearningTrainer.Core;
using EnglishLearningTrainer.Models;
using EnglishLearningTrainer.Services;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace EnglishLearningTrainer.ViewModels
{
    public class DictionaryManagementViewModel : TabViewModelBase
    {
        private readonly IDataService _dataService;
        private readonly Dictionary _dictionary;

        public ObservableCollection<Word> Words { get; set; }

        // Свойства для редактирования словаря
        public string DictionaryName { get; set; }
        public string Description { get; set; }
        public string LanguageFrom { get; set; }
        public string LanguageTo { get; set; }

        public ICommand SaveDictionaryCommand { get; }
        public ICommand DeleteDictionaryCommand { get; }
        public ICommand DeleteWordCommand { get; }
        public ICommand AddWordCommand { get; }
        public ICommand CloseCommand { get; }

        public DictionaryManagementViewModel(IDataService dataService, Dictionary dictionary)
        {
            _dataService = dataService;
            _dictionary = dictionary;
            Title = $"Управление: {dictionary.Name}";

            // Инициализируем свойства для редактирования
            DictionaryName = dictionary.Name;
            Description = dictionary.Description;
            LanguageFrom = dictionary.LanguageFrom;
            LanguageTo = dictionary.LanguageTo;

            Words = new ObservableCollection<Word>(dictionary.Words);

            // Команды
            SaveDictionaryCommand = new RelayCommand(async (param) => await SaveDictionaryAsync());
            DeleteDictionaryCommand = new RelayCommand(async (param) => await DeleteDictionaryAsync());
            DeleteWordCommand = new RelayCommand(async (param) => await DeleteWordAsync(param));
            AddWordCommand = new RelayCommand((param) => AddWord());
            CloseCommand = new RelayCommand((param) => Close());
        }

        private async Task SaveDictionaryAsync()
        {
            if (string.IsNullOrWhiteSpace(DictionaryName))
            {
                System.Diagnostics.Debug.WriteLine("Ошибка: не заполнено название словаря");
                return;
            }

            try
            {
                _dictionary.Name = DictionaryName.Trim();
                _dictionary.Description = Description?.Trim() ?? "";
                _dictionary.LanguageFrom = LanguageFrom.Trim();
                _dictionary.LanguageTo = LanguageTo.Trim();

                await _dataService.UpdateDictionaryAsync(_dictionary);
                System.Diagnostics.Debug.WriteLine($"Словарь '{DictionaryName}' успешно обновлен!");

                // Обновляем заголовок вкладки
                Title = $"Управление: {_dictionary.Name}";
                OnPropertyChanged(nameof(Title));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка при обновлении словаря: {ex.Message}");
            }
        }

        private async Task DeleteDictionaryAsync()
        {
            var result = System.Windows.MessageBox.Show(
                $"Вы уверены, что хотите удалить словарь '{_dictionary.Name}'? Это действие нельзя отменить.",
                "Подтверждение удаления",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    var success = await _dataService.DeleteDictionaryAsync(_dictionary.Id);
                    if (success)
                    {
                        System.Diagnostics.Debug.WriteLine($"Словарь '{_dictionary.Name}' удален!");

                        // Публикуем сообщение об обновлении данных
                        EventAggregator.Instance.Publish(new RefreshDataMessage());

                        // Закрываем вкладку
                        Close();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка при удалении словаря: {ex.Message}");
                }
            }
        }

        private async Task DeleteWordAsync(object parameter)
        {
            if (parameter is Word word)
            {
                var result = System.Windows.MessageBox.Show(
                    $"Удалить слово '{word.OriginalWord}'?",
                    "Подтверждение удаления",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    var success = await _dataService.DeleteWordAsync(word.Id);
                    if (success)
                    {
                        Words.Remove(word);
                        System.Diagnostics.Debug.WriteLine($"Слово '{word.OriginalWord}' удалено!");
                    }
                }
            }
        }

        private void AddWord()
        {
            var addWordVm = new AddWordViewModel(_dataService, _dictionary);
            EventAggregator.Instance.Publish(addWordVm);
        }

        private void Close()
        {
            EventAggregator.Instance.Publish(this);
        }
    }
}