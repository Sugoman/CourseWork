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
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LearningTrainer.ViewModels
{
    public class DashboardViewModel : TabViewModelBase
    {
        private readonly IDataService _dataService;
        private readonly User _currentUser;
        private readonly bool _isOnlineMode;
        private readonly IDialogService _dialogService;
        private readonly SettingsService _settingsService;
        private readonly PermissionService _permissionService;
        private readonly AccessNotificationService _notificationService;
        public ObservableCollection<DictionaryViewModel> Dictionaries { get; set; }
        public ObservableCollection<Rule> Rules { get; set; }
        public ObservableCollection<DictionaryViewModel> DisplayDictionaries { get; set; }

        public bool CanManage { get; }
        public AccessNotificationViewModel NotificationViewModel { get; private set; }

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
        public ICommand RefreshCommand { get; }
        public ICommand ShareRuleCommand { get; }

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

        private DashboardStats _stats;
        public DashboardStats Stats
        {
            get => _stats;
            set
            {
                if (SetProperty(ref _stats, value))
                {
                    UpdateCharts();
                }
            }
        }

        private ISeries[] _activitySeries = Array.Empty<ISeries>();
        public ISeries[] ActivitySeries
        {
            get => _activitySeries;
            set => SetProperty(ref _activitySeries, value);
        }

        private Axis[] _activityXAxis = Array.Empty<Axis>();
        public Axis[] ActivityXAxis
        {
            get => _activityXAxis;
            set => SetProperty(ref _activityXAxis, value);
        }

        private Axis[] _activityYAxis = Array.Empty<Axis>();
        public Axis[] ActivityYAxis
        {
            get => _activityYAxis;
            set => SetProperty(ref _activityYAxis, value);
        }

        private ISeries[] _knowledgeSeries = Array.Empty<ISeries>();
        public ISeries[] KnowledgeSeries
        {
            get => _knowledgeSeries;
            set => SetProperty(ref _knowledgeSeries, value);
        }

        private Axis[] _knowledgeXAxis = Array.Empty<Axis>();
        public Axis[] KnowledgeXAxis
        {
            get => _knowledgeXAxis;
            set => SetProperty(ref _knowledgeXAxis, value);
        }

        private Axis[] _knowledgeYAxis = Array.Empty<Axis>();
        public Axis[] KnowledgeYAxis
        {
            get => _knowledgeYAxis;
            set => SetProperty(ref _knowledgeYAxis, value);
        }

        private SolidColorPaint _legendTextPaint;
        public SolidColorPaint LegendTextPaint
        {
            get => _legendTextPaint;
            set => SetProperty(ref _legendTextPaint, value);
        }

        private void UpdateLegendTextPaint()
        {
            var colorResource = Application.Current.TryFindResource("PrimaryTextColor");
            if (colorResource is System.Windows.Media.Color mediaColor)
            {
                LegendTextPaint = new SolidColorPaint(new SKColor(mediaColor.R, mediaColor.G, mediaColor.B, mediaColor.A));
            }
            else
            {
                LegendTextPaint = new SolidColorPaint(SKColors.Black);
            }
        }

        private void UpdateCharts()
        {
            var stats = Stats ?? new DashboardStats();

            var today = DateTime.UtcNow.Date;
            var days = Enumerable.Range(0, 7)
                .Select(i => today.AddDays(-6 + i))
                .ToList();

            var activityLookup = stats.ActivityLast7Days
                ?.GroupBy(a => a.Date.Date)
                .ToDictionary(g => g.Key, g => g.First())
                ?? new Dictionary<DateTime, ActivityPoint>();

            var reviewed = days.Select(d => activityLookup.TryGetValue(d, out var v) ? v.Reviewed : 0).ToArray();
            var learned = days.Select(d => activityLookup.TryGetValue(d, out var v) ? v.Learned : 0).ToArray();

            ActivitySeries = new ISeries[]
            {
                new ColumnSeries<int>
                {
                    Name = "Повторено",
                    Values = reviewed,
                    Fill = new SolidColorPaint(new SKColor(59,130,246))
                },
                new LineSeries<int>
                {
                    Name = "Выучено",
                    Values = learned,
                    GeometrySize = 8,
                    Stroke = new SolidColorPaint(new SKColor(48,169,102), 3),
                    Fill = null
                }
            };

            ActivityXAxis = new[]
            {
                new Axis
                {
                    Labels = days.Select(d => d.ToString("dd.MM")).ToArray(),
                    LabelsRotation = 0
                }
            };
            ActivityYAxis = new[] { new Axis { MinLimit = 0 } };

            var distributionLookup = stats.KnowledgeDistribution
                ?.ToDictionary(k => k.Level, k => k.Count)
                ?? new Dictionary<int, int>();

            var levelValues = Enumerable.Range(0, 6)
                .Select(level => distributionLookup.TryGetValue(level, out var count) ? count : 0)
                .ToArray();

            KnowledgeSeries = new ISeries[]
            {
                new ColumnSeries<int>
                {
                    Name = "Уровень знаний",
                    Values = levelValues,
                    Fill = new SolidColorPaint(new SKColor(139,92,246))
                }
            };

            KnowledgeXAxis = new[]
            {
                new Axis
                {
                    Labels = new[] { "0", "1", "2", "3", "4", "5" }
                }
            };
            KnowledgeYAxis = new[] { new Axis { MinLimit = 0 } };
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
                var name = dictionaryVM.Name;
                Dictionaries.Remove(dictionaryVM);
                _notificationService.AddInfoNotification(
                    "Словарь удалён",
                    $"Словарь '{name}' был удалён");
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

            _notificationService.AddSuccessNotification(
                "Правило создано",
                $"Правило '{message.Rule.Title}' успешно добавлено");

            System.Diagnostics.Debug.WriteLine($"Rules collection updated: {Rules.Count} rules");
        }

        private void EditRule(object parameter)
        {
            if (parameter is Rule rule)
            {
                System.Diagnostics.Debug.WriteLine($"Opening EDITOR for: {rule.Title}");
                var managementVm = new RuleManagementViewModel(_dataService, _settingsService, rule, _currentUser.Id);
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

                    _notificationService.AddSuccessNotification(
                        "Импорт завершен",
                        $"Словарь '{savedDictionary.Name}' ({wordsToImport.Count} слов) успешно импортирован!");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка импорта: {ex.Message}");
                    _notificationService.AddErrorNotification(
                        "Ошибка импорта",
                        $"Не удалось импортировать словарь: {ex.Message}");
                }
            }
        }

        private void OnWordAdded(WordAddedMessage message)
        {
            var dictionaryVM = Dictionaries.FirstOrDefault(d => d.Id == message.DictionaryId);

            if (dictionaryVM != null)
            {
                dictionaryVM.Words.Add(message.Word);
                _notificationService.AddSuccessNotification(
                    "Слово добавлено",
                    $"'{message.Word.OriginalWord}' добавлено в словарь");
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

            ApplySorting();

            _notificationService.AddSuccessNotification(
                "Словарь создан",
                $"Словарь '{message.Dictionary.Name}' успешно добавлен");

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
                Stats = await _dataService.GetStatsAsync();

                OnPropertyChanged(nameof(Dictionaries));
            }
            catch (HttpRequestException httpEx)
            {
                if (httpEx.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    System.Diagnostics.Debug.WriteLine("!!! 401 (Unauthorized) ПОЙМАН в Dashboard. Запуск принудительного выхода");
                    _notificationService.AddErrorNotification(
                        "Сессия истекла",
                        "Требуется повторный вход в систему");
                    EventAggregator.Instance.Publish(new LogoutRequestedMessage());
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"!!! ОШИБКА HTTP в Dashboard: {httpEx.Message}");
                    _notificationService.AddErrorNotification(
                        "Ошибка соединения",
                        "Не удалось загрузить данные с сервера");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"!!! КРИТИЧЕСКАЯ ОШИБКА в Dashboard.LoadData: {ex.Message}");
                _notificationService.AddErrorNotification(
                    "Ошибка загрузки",
                    "Произошла ошибка при загрузке данных");
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
                var wordName = word.OriginalWord;
                var success = await _dataService.DeleteWordAsync(word.Id);
                if (success)
                {
                    var dictionary = Dictionaries.FirstOrDefault(d => d.Id == word.DictionaryId);
                    dictionary?.Words.Remove(word);
                    OnPropertyChanged(nameof(Dictionaries));
                    _notificationService.AddInfoNotification(
                        "Слово удалено",
                        $"'{wordName}' удалено из словаря");
                }
            }
        }

        private async Task DeleteRule(object parameter)
        {
            if (parameter is Rule rule)
            {
                var ruleName = rule.Title;
                var success = await _dataService.DeleteRuleAsync(rule.Id);
                if (success)
                {
                    Rules.Remove(rule);
                    OnPropertyChanged(nameof(Rules));
                    _notificationService.AddInfoNotification(
                        "Правило удалено",
                        $"'{ruleName}' удалено");
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
                    _notificationService.AddErrorNotification(
                        "Офлайн режим",
                        "Невозможно изменять словари в офлайн режиме");
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

        private SortKey _selectedSortKey;
        public SortKey SelectedSortKey
        {
            get => _selectedSortKey;
            set
            {
                if (SetProperty(ref _selectedSortKey, value))
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

            _permissionService = _isOnlineMode ? new PermissionService(_currentUser) : null;
            _notificationService = new AccessNotificationService();
            NotificationViewModel = new AccessNotificationViewModel(_currentUser, _notificationService);

            CanManage = _isOnlineMode
                && _currentUser?.Role != null
                && _currentUser.Role.Name != "Student";

            SetLocalizedTitle("Loc.Tab.Home");

            StartLearningCommand = new RelayCommand(async (param) => await StartLearning(param));
            OpenRuleCommand = new RelayCommand(OpenRule);
            EditRuleCommand = new RelayCommand(EditRule);
            CreateDictionaryCommand = new RelayCommand((param) =>
            {
                if (!NotificationViewModel.CheckPermissionAndNotify("Создать словарь", _permissionService?.CanCreateDictionary ?? true, "CreateDictionary"))
                    return;
                CreateDictionary();
            });
            CreateRuleCommand = new RelayCommand((param) =>
            {
                if (!NotificationViewModel.CheckPermissionAndNotify("Создать правило", _permissionService?.CanCreateRules ?? true, "CreateRule"))
                    return;
                CreateRule();
            });
            AddWordCommand = new RelayCommand((param) =>
            {
                if (!NotificationViewModel.CheckPermissionAndNotify("Добавить слово", _permissionService?.CanCreateDictionary ?? true, "AddWord"))
                    return;
                AddWord(param);
            });
            DeleteWordCommand = new RelayCommand(async (param) => await DeleteWord(param));
            DeleteRuleCommand = new RelayCommand(async (param) => await DeleteRule(param));
            ShareRuleCommand = new RelayCommand((param) =>
            {
                if (!NotificationViewModel.CheckPermissionAndNotify("Поделиться правилом", _permissionService?.CanShareRules ?? true, "ShareRule"))
                    return;
                ShareRule(param);
            });
            ManageDictionaryCommand = new RelayCommand((param) =>
            {
                if (!NotificationViewModel.CheckPermissionAndNotify("Управлять словарём", _permissionService?.CanEditDictionaries ?? true, "ManageDictionary"))
                    return;
                ManageDictionary(param);
            });
            ImportDictionaryCommand = new RelayCommand(async (param) =>
            {
                if (!NotificationViewModel.CheckPermissionAndNotify("Импортировать словарь", _permissionService?.CanCreateDictionary ?? true, "ImportDictionary"))
                    return;
                await ImportDictionary();
            });
            DisplayDictionaries = new ObservableCollection<DictionaryViewModel>();
            OpenSettingsCommand = new RelayCommand(OpenSettings);
            DisplaySortOptions = new ObservableCollection<SortingDisplayItem>()
            {
                new SortingDisplayItem { Key = SortKey.NameAsc, DisplayName = GetLocalized("Loc.Sort.NameAsc") },
                new SortingDisplayItem { Key = SortKey.NameDesc, DisplayName = GetLocalized("Loc.Sort.NameDesc") },
                new SortingDisplayItem { Key = SortKey.CountMin, DisplayName = GetLocalized("Loc.Sort.CountMin") },
                new SortingDisplayItem { Key = SortKey.CountMax, DisplayName = GetLocalized("Loc.Sort.CountMax") }
            };

            RefreshCommand = new RelayCommand((_) =>
            {
                System.Diagnostics.Debug.WriteLine(">>> Manual Refresh Triggered");
                LoadDataAsync();
            });
            EventAggregator.Instance.Subscribe<DictionaryDeletedMessage>(OnDictionaryDeleted);
            EventAggregator.Instance.Subscribe<RefreshDataMessage>(OnRefreshData);
            EventAggregator.Instance.Subscribe<RuleAddedMessage>(OnRuleAdded);
            EventAggregator.Instance.Subscribe<DictionaryAddedMessage>(OnDictionaryAdded);
            EventAggregator.Instance.Subscribe<WordAddedMessage>(OnWordAdded);
            // ShowNotificationMessage is now handled globally in ShellViewModel

            UpdateLegendTextPaint();
            _settingsService.MarkdownConfigChanged += OnThemeChanged;
            LoadDataAsync();
        }

        private void OnThemeChanged(MarkdownConfig config)
        {
            UpdateLegendTextPaint();
        }
    }
}
