using EnglishLearningTrainer.Core;
using EnglishLearningTrainer.Models;
using EnglishLearningTrainer.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace EnglishLearningTrainer.ViewModels
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

            StartLearningCommand = new RelayCommand(StartLearning);
            OpenRuleCommand = new RelayCommand(OpenRule);

            CreateDictionaryCommand = new RelayCommand((param) => CreateDictionary());
            CreateRuleCommand = new RelayCommand((param) => CreateRule());
            AddWordCommand = new RelayCommand((param) => AddWord(param));
            DeleteWordCommand = new RelayCommand(async (param) => await DeleteWord(param));
            DeleteRuleCommand = new RelayCommand(async (param) => await DeleteRule(param));
            ManageDictionaryCommand = new RelayCommand((param) => ManageDictionary(param));

            EventAggregator.Instance.Subscribe<RuleAddedMessage>(OnRuleAdded);
            EventAggregator.Instance.Subscribe<DictionaryAddedMessage>(OnDictionaryAdded);
            EventAggregator.Instance.Subscribe<WordAddedMessage>(OnWordAdded);


            LoadDataAsync();
        }
        private void OnRuleAdded(RuleAddedMessage message)
        {
            System.Diagnostics.Debug.WriteLine($"=== RULE ADDED: {message.Rule.Title} ===");

            // Добавляем правило в коллекцию
            Rules.Add(message.Rule);

            // Сортируем правила по названию (опционально)
            var sortedRules = Rules.OrderBy(r => r.Title).ToList();
            Rules.Clear();
            foreach (var rule in sortedRules)
            {
                Rules.Add(rule);
            }

            System.Diagnostics.Debug.WriteLine($"Rules collection updated: {Rules.Count} rules");
        }

        // ОБРАБОТЧИК ДОБАВЛЕНИЯ СЛОВАРЯ
        private void OnDictionaryAdded(DictionaryAddedMessage message)
        {
            System.Diagnostics.Debug.WriteLine($"=== DICTIONARY ADDED: {message.Dictionary.Name} ===");

            Dictionaries.Add(message.Dictionary);

            // Сортируем словари по названию
            var sortedDicts = Dictionaries.OrderBy(d => d.Name).ToList();
            Dictionaries.Clear();
            foreach (var dict in sortedDicts)
            {
                Dictionaries.Add(dict);
            }

            System.Diagnostics.Debug.WriteLine($"Dictionaries collection updated: {Dictionaries.Count} dictionaries");
        }

        // ОБРАБОТЧИК ДОБАВЛЕНИЯ СЛОВА
        private void OnWordAdded(WordAddedMessage message)
        {
            System.Diagnostics.Debug.WriteLine($"=== WORD ADDED: {message.Word.OriginalWord} to dictionary {message.DictionaryId} ===");

            // Находим словарь и добавляем слово
            var dictionary = Dictionaries.FirstOrDefault(d => d.Id == message.DictionaryId);
            if (dictionary != null && dictionary.Words != null)
            {
                dictionary.Words.Add(message.Word);

                // Обновляем отображение счетчика слов
                OnPropertyChanged(nameof(Dictionaries));
            }
        }

        private async void LoadDataAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=== LOAD DATA STARTED ===");
                System.Diagnostics.Debug.WriteLine($"Dictionaries is null: {Dictionaries == null}");
                System.Diagnostics.Debug.WriteLine($"Rules is null: {Rules == null}");
                System.Diagnostics.Debug.WriteLine($"_dataService is null: {_dataService == null}");

                // Загрузка данных через сервис
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

                System.Diagnostics.Debug.WriteLine("Adding dictionaries...");
                if (dictionaries != null)
                {
                    foreach (var dict in dictionaries)
                    {
                        dict.Words = dict.Words ?? new List<Word>();
                        Dictionaries.Add(dict);
                        System.Diagnostics.Debug.WriteLine($"Added dictionary: {dict.Name}");
                    }
                }

                System.Diagnostics.Debug.WriteLine("Adding rules...");
                if (rules != null)
                {
                    foreach (var rule in rules)
                    {
                        Rules.Add(rule);
                        System.Diagnostics.Debug.WriteLine($"Added rule: {rule.Title}");
                    }
                }

                // Уведомляем об изменении для обновления интерфейса
                OnPropertyChanged(nameof(Dictionaries));
                OnPropertyChanged(nameof(Rules));

                System.Diagnostics.Debug.WriteLine($"Load completed: {Dictionaries.Count} dictionaries, {Rules.Count} rules");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CRITICAL ERROR in LoadDataAsync: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");

                // Аварийная инициализация если что-то пошло не так
                if (Dictionaries == null) Dictionaries = new ObservableCollection<Dictionary>();
                if (Rules == null) Rules = new ObservableCollection<Rule>();
            }
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

        public void StartLearning(object parameter)
        {
            if (parameter is Dictionary dictionary)
            {
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
                    // Удаляем из коллекции
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