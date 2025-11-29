using LearningTrainer.Core;
using LearningTrainer.Services;
using LearningTrainer.Services.Dialogs;
using LearningTrainerShared.Models;
using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Input;

namespace LearningTrainer.ViewModels
{
    public class DashboardViewModel : TabViewModelBase
    {
        private readonly IDataService _dataService;
        private readonly User _currentUser;
        private readonly bool _isOnlineMode;
        private readonly IDialogService _dialogService;
        private readonly SettingsService _settingsService;
        public ObservableCollection<DictionaryViewModel> Dictionaries { get; set; }
        public ObservableCollection<Rule> Rules { get; set; }
        public ObservableCollection<DictionaryViewModel> DisplayDictionaries { get; set; }

        public bool CanManage { get; }

        public ICommand OpenSettingsCommand { get; }
        public ICommand CreateDictionaryCommand { get; }
        public ICommand ImportDictionaryCommand { get; }
        public ICommand CreateRuleCommand { get; }
        public ICommand AddWordCommand { get; }
        public ICommand StartLearningCommand { get; }
        public ICommand OpenRuleCommand { get; }
        public ICommand DeleteWordCommand { get; }
        public ICommand DeleteRuleCommand { get; }
        public ICommand ManageDictionaryCommand { get; }
        public ICommand EditRuleCommand { get; }

        private bool _isOverviewMode;
        public bool IsOverviewMode
        {
            get => _isOverviewMode;
            set => SetProperty(ref _isOverviewMode, value);
        }
        public bool IsContentMode => !IsOverviewMode;
        private string _searchQuery;
        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                if (SetProperty(ref _searchQuery, value))
                {
                    ApplySorting();
                }
            }
        }

        private bool _isFeatured;

        public bool IsFeatured
        {
            get => _isFeatured;
            set => SetProperty(ref _isFeatured, value);
        }

        public DashboardViewModel(User? user, IDataService dataService, SettingsService settingsService)
        {
            IsOverviewMode = false;

            Dictionaries = new ObservableCollection<DictionaryViewModel>();
            Rules = new ObservableCollection<Rule>();
            _dataService = dataService;
            _currentUser = user;
            _dialogService = new DialogService();
            _settingsService = settingsService;
            _isOnlineMode = (_currentUser != null);

            CanManage = _isOnlineMode
            && _currentUser.Role != null
            && _currentUser.Role.Name != "Student";

            Title = "HOME";

            StartLearningCommand = new RelayCommand(async (param) => await StartLearning(param));
            OpenRuleCommand = new RelayCommand(OpenRule);
            EditRuleCommand = new RelayCommand(EditRule);
            CreateDictionaryCommand = new RelayCommand((param) => CreateDictionary());
            CreateRuleCommand = new RelayCommand((param) => CreateRule());
            AddWordCommand = new RelayCommand((param) => AddWord(param));
            DeleteWordCommand = new RelayCommand(async (param) => await DeleteWord(param));
            DeleteRuleCommand = new RelayCommand(async (param) => await DeleteRule(param));
            ManageDictionaryCommand = new RelayCommand((param) => ManageDictionary(param));
            ImportDictionaryCommand = new RelayCommand(async (param) => await ImportDictionary());
            DisplayDictionaries = new ObservableCollection<DictionaryViewModel>();
            OpenSettingsCommand = new RelayCommand(OpenSettings);
            DisplaySortOptions = new ObservableCollection<SortingDisplayItem>()
            {
                new SortingDisplayItem { Key = SortKey.NameAsc, DisplayName = GetLocalized("Loc.Sort.NameAsc") },
                new SortingDisplayItem { Key = SortKey.NameDesc, DisplayName = GetLocalized("Loc.Sort.NameDesc") },
                new SortingDisplayItem { Key = SortKey.CountMin, DisplayName = GetLocalized("Loc.Sort.CountMin") },
                new SortingDisplayItem { Key = SortKey.CountMax, DisplayName = GetLocalized("Loc.Sort.CountMax") }
            };


            EventAggregator.Instance.Subscribe<DictionaryDeletedMessage>(OnDictionaryDeleted);
            EventAggregator.Instance.Subscribe<RefreshDataMessage>(OnRefreshData);
            EventAggregator.Instance.Subscribe<RuleAddedMessage>(OnRuleAdded);
            EventAggregator.Instance.Subscribe<DictionaryAddedMessage>(OnDictionaryAdded);
            EventAggregator.Instance.Subscribe<WordAddedMessage>(OnWordAdded);
            LoadDataAsync();
        }
        private void OpenSettings(object obj)
        {
            EventAggregator.Instance.Publish(new SettingsViewModel(_settingsService, _dataService, _currentUser));
        }
        private void OnDictionaryDeleted(DictionaryDeletedMessage message)
        {
            var dictionaryVM = Dictionaries.FirstOrDefault(d => d.Id == message.DictionaryId);

            if (dictionaryVM != null)
            {
                Dictionaries.Remove(dictionaryVM);
                System.Diagnostics.Debug.WriteLine($"Словарь ID {message.DictionaryId} удален из коллекции Dashboard.");
            }
        }

        private void OnRuleAdded(RuleAddedMessage message)
        {
            System.Diagnostics.Debug.WriteLine($"=== RULE ADDED: {message.Rule.Title} ===");

            Rules.Add(message.Rule);

            var sortedRules = Rules.OrderBy(r => r.Title).ToList();
            Rules.Clear();
            foreach (var rule in sortedRules)
            {
                Rules.Add(rule);
            }

            System.Diagnostics.Debug.WriteLine($"Rules collection updated: {Rules.Count} rules");
        }

        private void EditRule(object parameter)
        {
            if (parameter is Rule rule)
            {
                System.Diagnostics.Debug.WriteLine($"Opening EDITOR for: {rule.Title}");
                var managementVm = new RuleManagementViewModel(_dataService, rule, _currentUser.Id);
                EventAggregator.Instance.Publish(managementVm);
            }
        }

        private async Task ImportDictionary()
        {
            if (_dialogService.ShowOpenDialog(out string filePath))
            {
                try
                {
                    string json = await File.ReadAllTextAsync(filePath);

                    var options = new JsonSerializerOptions
                    {
                        ReferenceHandler = ReferenceHandler.Preserve,
                        PropertyNameCaseInsensitive = true
                    };

                    var newDictionary = JsonSerializer.Deserialize<Dictionary>(json, options);

                    newDictionary.Id = 0;
                    var wordsToImport = newDictionary.Words.ToList();
                    newDictionary.Words.Clear();

                    var savedDictionary = await _dataService.AddDictionaryAsync(newDictionary);

                    foreach (var word in wordsToImport)
                    {
                        word.Id = 0;
                        word.DictionaryId = savedDictionary.Id;
                        await _dataService.AddWordAsync(word);
                    }
                    EventAggregator.Instance.Publish(new DictionaryAddedMessage(savedDictionary));
                    EventAggregator.Instance.Publish(new RefreshDataMessage());

                    System.Windows.MessageBox.Show(
                        $"Словарь '{savedDictionary.Name}' ({wordsToImport.Count} слов) успешно импортирован!",
                        "Импорт завершен",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка импорта: {ex.Message}");
                    System.Windows.MessageBox.Show(
                        $"Не удалось импортировать словарь. Убедитесь, что это корректный .json файл.\nОшибка: {ex.Message}",
                        "Ошибка импорта",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

        private void OnWordAdded(WordAddedMessage message)
        {
            var dictionaryVM = Dictionaries.FirstOrDefault(d => d.Id == message.DictionaryId);

            if (dictionaryVM != null)
            {
                dictionaryVM.Words.Add(message.Word);
            }
        }

        private void OnDictionaryAdded(DictionaryAddedMessage message)
        {
            System.Diagnostics.Debug.WriteLine($"=== DICTIONARY ADDED: {message.Dictionary.Name} ===");

            Dictionaries.Add(new DictionaryViewModel(message.Dictionary));

            var sortedDicts = Dictionaries.OrderBy(d => d.Name).ToList();
            Dictionaries.Clear();
            foreach (var dict in sortedDicts)
            {
                Dictionaries.Add(dict);
            }

            System.Diagnostics.Debug.WriteLine($"Dictionaries collection updated: {Dictionaries.Count} dictionaries");
        }

        private async void LoadDataAsync()
        {

            try
            {
                SelectedSortKey = SortKey.NameAsc;
                List<Dictionary> dictionaries;
                List<Rule> rules;


                if (_currentUser?.Role?.Name == "Student")
                {
                    dictionaries = await _dataService.GetAvailableDictionariesAsync();
                    rules = await _dataService.GetAvailableRulesAsync();
                }
                else
                {
                    dictionaries = await _dataService.GetDictionariesAsync();
                    rules = await _dataService.GetRulesAsync();
                }

                System.Diagnostics.Debug.WriteLine("Clearing collections...");
                Dictionaries.Clear();
                Rules.Clear();

                foreach (var dict in dictionaries)
                {
                    Dictionaries.Add(new DictionaryViewModel(dict));
                }
                foreach (var rule in rules) Rules.Add(rule);

                DisplayDictionaries.Clear();
                foreach (var dict in Dictionaries)
                {
                    DisplayDictionaries.Add(dict);
                }

                OnPropertyChanged(nameof(Dictionaries));
            }
            catch (HttpRequestException httpEx)
            {
                if (httpEx.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    System.Diagnostics.Debug.WriteLine("!!! 401 (Unauthorized) ПОЙМАН в Dashboard. Запуск принудительного выхода");
                    EventAggregator.Instance.Publish(new LogoutRequestedMessage());
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"!!! ОШИБКА HTTP в Dashboard: {httpEx.Message}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"!!! КРИТИЧЕСКАЯ ОШИБКА в Dashboard.LoadData: {ex.Message}");
            }
        }

        private void CreateDictionary()
        {
            var addDictionaryVm = new AddDictionaryViewModel(_dataService);
            EventAggregator.Instance.Publish(addDictionaryVm);
        }

        private void CreateRule()
        {
            var addRuleVm = new AddRuleViewModel(_dataService, _settingsService);
            EventAggregator.Instance.Publish(addRuleVm);
        }

        private void AddWord(object parameter)
        {
            if (parameter is DictionaryViewModel dictionaryVM)
            {
                var addWordVm = new AddWordViewModel(_dataService, dictionaryVM.Model);
                EventAggregator.Instance.Publish(addWordVm);
            }
        }

        private void OpenRule(object parameter)
        {
            if (parameter is Rule rule)
            {
                System.Diagnostics.Debug.WriteLine($"Opening VIEWER for: {rule.Title}");

                var viewerVm = new RuleViewModel(rule, _settingsService);
                EventAggregator.Instance.Publish(viewerVm);
            }
        }

        private void OnRefreshData(RefreshDataMessage message)
        {

            System.Diagnostics.Debug.WriteLine(">>> REFRESH. Перезагрузка данных...");
            LoadDataAsync();
        }

        public async Task StartLearning(object parameter)
        {
            if (parameter is DictionaryViewModel dictionaryVM)
            {
                var learningVM = new LearningViewModel(_dataService, dictionaryVM.Id, dictionaryVM.Name);

                EventAggregator.Instance.Publish(learningVM);
            }
        }

        private async Task DeleteWord(object parameter)
        {
            if (parameter is Word word)
            {
                var success = await _dataService.DeleteWordAsync(word.Id);
                if (success)
                {
                    var dictionary = Dictionaries.FirstOrDefault(d => d.Id == word.DictionaryId);
                    dictionary?.Words.Remove(word);
                    OnPropertyChanged(nameof(Dictionaries));
                }
            }
        }

        private async Task DeleteRule(object parameter)
        {
            if (parameter is Rule rule)
            {
                var success = await _dataService.DeleteRuleAsync(rule.Id);
                if (success)
                {
                    Rules.Remove(rule);
                    OnPropertyChanged(nameof(Rules));
                }
            }
        }

        public enum SortKey
        {
            NameAsc,    // По названию (А-Я)
            NameDesc,   // По названию (Я-А)
            CountMin,   // По количеству слов (Мин)
            CountMax    // По количеству слов (Макс)
        }
        private static string GetLocalized(string key)
        {
            try
            {
                return Application.Current.FindResource(key) as string;
            }
            catch
            {
                return $"MISSING_LOC:{key}";
            }
        }
        private void ManageDictionary(object parameter)
        {
            if (parameter is DictionaryViewModel dictionaryVM)
            {
                if (_currentUser == null)
                {
                    MessageBox.Show("Невозможно изменять словари в офлайн режиме");
                }
                else
                {
                    int currentUserId = _currentUser.Id;

                    var managementVm = new DictionaryManagementViewModel(
                        _dataService,
                        dictionaryVM.Model,
                        dictionaryVM.Words,
                        currentUserId
                    );
                    EventAggregator.Instance.Publish(managementVm);

                }
            }
        }

        public class SortingDisplayItem
        {
            public SortKey Key { get; set; }
            public string DisplayName { get; set; }
        }

        private void ShareRule(object param)
        {
            if (param is Rule rule)
            {
                var shareVm = new ShareContentViewModel(
                    _dataService,
                    rule.Id,
                    rule.Title,
                    ShareContentType.Rule
                );
                EventAggregator.Instance.Publish(shareVm);
            }
        }

        public ObservableCollection<SortingDisplayItem> DisplaySortOptions { get; }

        // 2. Выбранный элемент (SelectedValue)
        private SortKey _selectedSortKey;
        public SortKey SelectedSortKey
        {
            get => _selectedSortKey;
            set
            {
                if (SetProperty(ref _selectedSortKey, value)) // Assuming SetProperty handles INPC
                {
                    ApplySorting();
                }
            }
        }

        private void ApplySorting()
        {
            if (Dictionaries == null || DisplaySortOptions == null) return;

            IEnumerable<DictionaryViewModel> query = Dictionaries;

            switch (SelectedSortKey)
            {
                case SortKey.NameAsc:
                    query = query.OrderBy(d => d.Name);
                    break;
                case SortKey.NameDesc:
                    query = query.OrderByDescending(d => d.Name);
                    break;
                case SortKey.CountMin:
                    query = query.OrderBy(d => d.WordCount);
                    break;
                case SortKey.CountMax:
                    query = query.OrderByDescending(d => d.WordCount);
                    break;
            }


            if (DisplayDictionaries == null)
                DisplayDictionaries = new ObservableCollection<DictionaryViewModel>();

            DisplayDictionaries.Clear();
            foreach (var item in query)
            {
                DisplayDictionaries.Add(item);
            }
        }
    }
}