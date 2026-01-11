using LearningTrainer.Core;
using LearningTrainer.Services;
using LearningTrainer.Services.Dialogs;
using LearningTrainerShared.Models;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using System.Windows;
using System.Windows.Input;
using static LearningTrainer.Core.EventAggregator;

namespace LearningTrainer.ViewModels
{
    public class DictionaryManagementViewModel : TabViewModelBase
    {
        private readonly IDataService _dataService;
        private readonly IDialogService _dialogService;
        private readonly Dictionary _dictionary;
        private readonly Dictionary _dictionaryModel;
        private bool _isReadOnly;
        public bool IsReadOnly
        {
            get => _isReadOnly;
            set => SetProperty(ref _isReadOnly, value);
        }

        public bool IsEditable => !IsReadOnly;

        private string _dictionaryName;
        public string DictionaryName
        {
            get => _dictionaryName;
            set
            {
                if (SetProperty(ref _dictionaryName, value))
                {
                }
            }
        }

        private string _description;
        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        public string LanguageFrom => _dictionaryModel.LanguageFrom ?? "Unknown";
        public string LanguageTo => _dictionaryModel.LanguageTo ?? "Unknown";


        private ObservableCollection<Word> _allWords;
        public ObservableCollection<Word> Words { get; set; }


        private string _searchWordQuery;
        public string SearchWordQuery
        {
            get => _searchWordQuery;
            set
            {
                if (SetProperty(ref _searchWordQuery, value))
                {
                    FilterWords();
                }
            }
        }

        public ICommand AddWordCommand { get; }
        public ICommand DeleteWordCommand { get; }
        public ICommand DeleteDictionaryCommand { get; }
        public ICommand ExportDictionaryCommand { get; }
        public ICommand SaveChangesCommand { get; }
        public ICommand CloseCommand { get; }
        public ICommand ShareDictionaryCommand { get; }



        public DictionaryManagementViewModel(IDataService dataService,
                                             Dictionary dictionary,
                                             ObservableCollection<Word> words,
                                             int currentUserId)
        {
            _dataService = dataService;
            _dictionaryModel = dictionary;
            _dictionary = dictionary;
            _dialogService = new DialogService();

            DictionaryName = dictionary.Name;
            Description = dictionary.Description;
            IsReadOnly = dictionary.UserId != currentUserId;

            System.Diagnostics.Debug.WriteLine($"UserId: {dictionary.UserId}, Current: {currentUserId}, IsReadOnly: {IsReadOnly}, IsEditable: {IsEditable}");
           
            SetLocalizedTitle("Loc.Tab.EditDictionary", $": {dictionary.Name}");

            _allWords = words;
            Words = new ObservableCollection<Word>(words);

            AddWordCommand = new RelayCommand(AddWord, (_) => IsEditable);
            DeleteWordCommand = new RelayCommand(async (p) => await DeleteWord(p), (_) => IsEditable);
            SaveChangesCommand = new RelayCommand(async (p) =>
            {
                System.Diagnostics.Debug.WriteLine(">>> SAVE COMMAND EXECUTED <<<"); 
                await SaveChanges();
            }, (_) => IsEditable);

            DeleteDictionaryCommand = new RelayCommand(async (p) => await DeleteDictionary(), (_) => IsEditable);
            ShareDictionaryCommand = new RelayCommand(ShareDictionary);
            ExportDictionaryCommand = new RelayCommand((param) => ExportDictionary());


            CloseCommand = new RelayCommand(CloseTab);
            EventAggregator.Instance.Subscribe<WordAddedMessage>(OnWordAdded);
        }
        private void ShareDictionary(object obj)
        {
            var shareVm = new ShareContentViewModel(
                _dataService,
                _dictionaryModel.Id,
                _dictionaryModel.Name,
                ShareContentType.Dictionary
            );

            EventAggregator.Instance.Publish(shareVm);
        }

        private void FilterWords()
        {
            if (string.IsNullOrWhiteSpace(SearchWordQuery))
            {
                Words.Clear();
                foreach (var word in _allWords) Words.Add(word);
            }
            else
            {
                var filtered = _allWords.Where(w =>
                    w.OriginalWord.Contains(SearchWordQuery, StringComparison.OrdinalIgnoreCase) ||
                    w.Translation.Contains(SearchWordQuery, StringComparison.OrdinalIgnoreCase)
                ).ToList();

                Words.Clear();
                foreach (var word in filtered) Words.Add(word);
            }
        }

        private async Task SaveChanges()
        {
            _dictionaryModel.Name = DictionaryName;
            _dictionaryModel.Description = Description;

            try
            {
                var dictionaryToUpdate = new Dictionary
                {
                    Id = _dictionaryModel.Id,
                    Name = DictionaryName,
                    Description = Description,
                    LanguageFrom = _dictionaryModel.LanguageFrom,
                    LanguageTo = _dictionaryModel.LanguageTo,
                    UserId = _dictionaryModel.UserId,

                    Words = new List<Word>(),

                    User = null
                };

                bool isSuccess = await _dataService.UpdateDictionaryAsync(dictionaryToUpdate);

                if (isSuccess)
                {
                    EventAggregator.Instance.Publish(ShowNotificationMessage.Success(
                        "Сохранено",
                        "Изменения успешно сохранены!"));
                    TitleSuffix = $": {DictionaryName}";
                    UpdateLocalizedTitle();
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("UpdateDictionaryAsync returned FALSE.");
                    EventAggregator.Instance.Publish(ShowNotificationMessage.Error(
                        "Ошибка сервера",
                        "Не удалось сохранить изменения"));
                }
            }
            catch (System.Exception ex)
            {
                EventAggregator.Instance.Publish(ShowNotificationMessage.Error(
                    "Критическая ошибка",
                    ex.Message));
            }
        }


        private void AddWord(object obj)
        {
            var addWordVm = new AddWordViewModel(_dataService, _dictionaryModel);
            EventAggregator.Instance.Publish(addWordVm);
        }

        private void OnWordAdded(WordAddedMessage message)
        {
            if (message.DictionaryId == _dictionaryModel.Id)
            {
                if (!_allWords.Contains(message.Word))
                {
                    _allWords.Add(message.Word);
                }

                FilterWords();
            }
        }

        private async Task DeleteWord(object parameter)
        {
            if (parameter is Word word)
            {
                if (MessageBox.Show($"Delete word '{word.OriginalWord}'?", "Confirm", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    var success = await _dataService.DeleteWordAsync(word.Id);
                    if (success)
                    {
                        _allWords.Remove(word);
                        FilterWords();
                    }
                }
            }
        }

        private async Task DeleteDictionary()
        {
            if (MessageBox.Show($"А ты уверен, что хочешь удалить словарь '{DictionaryName}'?", "УДАЛЕНИЕ СЛОВАРЯ", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                var success = await _dataService.DeleteDictionaryAsync(_dictionaryModel.Id);
                if (success)
                {
                    EventAggregator.Instance.Publish(new DictionaryDeletedMessage(_dictionaryModel.Id));
                    EventAggregator.Instance.Publish(new CloseTabMessage(this));
                }
            }
        }

        private void ExportDictionary()
        {
            string defaultName = $"dictionary-{_dictionary.Name.Replace(" ", "-")}.json";

            if (_dialogService.ShowSaveDialog(defaultName, out string filePath))
            {
                try
                {
                    var options = new JsonSerializerOptions
                    {
                        ReferenceHandler = ReferenceHandler.Preserve,
                        WriteIndented = true,
                        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
                    };

                    string json = JsonSerializer.Serialize(_dictionary, options);

                    File.WriteAllText(filePath, json);

                    EventAggregator.Instance.Publish(ShowNotificationMessage.Success(
                        "Экспорт завершен",
                        $"Словарь '{_dictionary.Name}' успешно экспортирован!"));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка экспорта: {ex.Message}");
                    EventAggregator.Instance.Publish(ShowNotificationMessage.Error(
                        "Ошибка экспорта",
                        $"Произошла ошибка: {ex.Message}"));
                }
            }
        }

        private void CloseTab(object obj)
        {
            EventAggregator.Instance.Publish(new CloseTabMessage(this));
        }
    }
}