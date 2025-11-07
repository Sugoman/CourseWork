using LearningTrainer.Core;
using LearningTrainer.Models;
using LearningTrainer.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace LearningTrainer.ViewModels
{
    public class DashboardViewModel : TabViewModelBase
    {
        private readonly IDataService _dataService;
        private readonly User _currentUser;        
        private readonly bool _isOnlineMode;
        public ObservableCollection<Dictionary> Dictionaries { get; set; }
        public ObservableCollection<Rule> Rules { get; set; }
        public bool CanManage { get; }

        public ICommand CreateDictionaryCommand { get; }
        public ICommand CreateRuleCommand { get; }
        public ICommand AddWordCommand { get; }
        public ICommand StartLearningCommand { get; }
        public ICommand OpenRuleCommand { get; }
        public ICommand DeleteWordCommand { get; }
        public ICommand DeleteRuleCommand { get; }
        public ICommand ManageDictionaryCommand { get; }

        public DashboardViewModel(User? user, IDataService dataService)
        {
            Dictionaries = new ObservableCollection<Dictionary>();
            Rules = new ObservableCollection<Rule>();
            _dataService = dataService;
            _currentUser = user;

            _isOnlineMode = (_currentUser != null);

            CanManage = _isOnlineMode                 
            && _currentUser.Role != null              
            && _currentUser.Role.Name != "Student";

            Title = "Мой Дашборд";

            StartLearningCommand = new RelayCommand(async (param) => await StartLearning(param));
            OpenRuleCommand = new RelayCommand(OpenRule);

            CreateDictionaryCommand = new RelayCommand((param) => CreateDictionary());
            CreateRuleCommand = new RelayCommand((param) => CreateRule());
            AddWordCommand = new RelayCommand((param) => AddWord(param));
            DeleteWordCommand = new RelayCommand(async (param) => await DeleteWord(param));
            DeleteRuleCommand = new RelayCommand(async (param) => await DeleteRule(param));
            ManageDictionaryCommand = new RelayCommand((param) => ManageDictionary(param));

            EventAggregator.Instance.Subscribe<RefreshDataMessage>(OnRefreshData);
            EventAggregator.Instance.Subscribe<RuleAddedMessage>(OnRuleAdded);
            EventAggregator.Instance.Subscribe<DictionaryAddedMessage>(OnDictionaryAdded);


            LoadDataAsync();
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

        private void OnDictionaryAdded(DictionaryAddedMessage message)
        {
            System.Diagnostics.Debug.WriteLine($"=== DICTIONARY ADDED: {message.Dictionary.Name} ===");

            Dictionaries.Add(message.Dictionary);

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
            var dictionaries = await _dataService.GetDictionariesAsync();
            var rules = await _dataService.GetRulesAsync();

                System.Diagnostics.Debug.WriteLine($"dictionaries received: {dictionaries != null}, count: {dictionaries?.Count}");
                System.Diagnostics.Debug.WriteLine($"rules received: {rules != null}, count: {rules?.Count}");

                // ПРОВЕРКА ПЕРЕД ОЧИСТКОЙ
                if (Dictionaries == null)
                {
                    System.Diagnostics.Debug.WriteLine("ERROR: Dictionaries collection is null!");
                    Dictionaries = new ObservableCollection<Dictionary>();
                }

                if (Rules == null)
                {
                    System.Diagnostics.Debug.WriteLine("ERROR: Rules collection is null!");
                    Rules = new ObservableCollection<Rule>();
                }

                System.Diagnostics.Debug.WriteLine("Clearing collections...");
                Dictionaries.Clear();
                Rules.Clear();

            foreach (var dict in dictionaries)
            {
                Dictionaries.Add(dict);
                dict.Words = dict.Words ?? new List<Word>();
            }
            foreach (var rule in rules) Rules.Add(rule);

            OnPropertyChanged(nameof(Dictionaries));
        }

        private void CreateDictionary()
        {
            var addDictionaryVm = new AddDictionaryViewModel(_dataService);
            EventAggregator.Instance.Publish(addDictionaryVm);
        }

        private void CreateRule()
        {
            var addRuleVm = new AddRuleViewModel(_dataService);
            EventAggregator.Instance.Publish(addRuleVm);
        }

        private void AddWord(object parameter)
        {
            if (parameter is Dictionary dictionary)
            {
                var addWordVm = new AddWordViewModel(_dataService, dictionary);
                EventAggregator.Instance.Publish(addWordVm);
            }
        }

        private void OpenRule(object parameter)
        {
            if (parameter is Rule rule)
            {
                EventAggregator.Instance.Publish(new RuleViewModel(rule));
            }
        }

        private void OnRefreshData(RefreshDataMessage message)
        {

            System.Diagnostics.Debug.WriteLine(">>> REFRESH. Перезагрузка данных...");
            LoadDataAsync();
        }

        public async Task StartLearning(object parameter)
        {
            if (parameter is Dictionary dictionary)
            {
                if (dictionary.Words == null || dictionary.Words.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine($">>> Пустой словарь. ID: {dictionary.Id}");
                    dictionary = await _dataService.GetDictionaryByIdAsync(dictionary.Id);
                }

                // 3. ЗАПУСК: (Теперь у нас 'полный' словарь со словами)
                EventAggregator.Instance.Publish(new LearningViewModel(dictionary));
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
        private void ManageDictionary(object parameter)
        {
            if (parameter is Dictionary dictionary)
            {
                var managementVm = new DictionaryManagementViewModel(_dataService, dictionary);
                EventAggregator.Instance.Publish(managementVm);
            }
        }
    }
}