using EnglishLearningTrainer.Core;
using EnglishLearningTrainer.Models;
using EnglishLearningTrainer.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;
using static EnglishLearningTrainer.Core.EventAggregator;

namespace EnglishLearningTrainer.ViewModels
{
    public class ShellViewModel : ObservableObject
    {
        private TabViewModelBase _selectedTab;
        public TabViewModelBase SelectedTab
        {
            get => _selectedTab;
            set => SetProperty(ref _selectedTab, value);
        }

        public ObservableCollection<TabViewModelBase> Tabs { get; }
        private readonly User? _currentUser;
        private readonly IDataService _dataService;
        public ICommand CloseTabCommand { get; }
        public ICommand LogoutCommand { get; }

        public ShellViewModel(User? user, IDataService dataService, TabViewModelBase initialDashboard)
        {
            _currentUser = user;
            _dataService = dataService;

            Tabs = new ObservableCollection<TabViewModelBase>();
            CloseTabCommand = new RelayCommand(CloseTab, CanCloseTab);
            LogoutCommand = new RelayCommand(PerformLogout);

            EventAggregator.Instance.Subscribe<LearningViewModel>(OpenTab);
            EventAggregator.Instance.Subscribe<RuleViewModel>(OpenTab);
            EventAggregator.Instance.Subscribe<AddDictionaryViewModel>(OpenTab);
            EventAggregator.Instance.Subscribe<AddRuleViewModel>(OpenTab);
            EventAggregator.Instance.Subscribe<AddWordViewModel>(OpenTab);
            EventAggregator.Instance.Subscribe<CloseTabMessage>(HandleCloseTabMessage);
            EventAggregator.Instance.Subscribe<DictionaryManagementViewModel>(OpenTab);


            // Передаем сервис в конструктор
            var dashboard = initialDashboard;
            Tabs.Add(dashboard);
            SelectedTab = dashboard;
        }
        private void PerformLogout(object obj)
        {
            // Просто "кричим" в систему
            EventAggregator.Instance.Publish(new LogoutRequestedMessage());
        }
        private void OpenTab(TabViewModelBase tab)
        {
            System.Diagnostics.Debug.WriteLine($"=== OPEN TAB METHOD CALLED ===");
            System.Diagnostics.Debug.WriteLine($"Tab: {tab?.GetType().Name}");
            System.Diagnostics.Debug.WriteLine($"Tab Title: {tab?.Title}");
            System.Diagnostics.Debug.WriteLine($"Tabs collection: {Tabs?.Count} items");
            System.Diagnostics.Debug.WriteLine($"SelectedTab before: {SelectedTab?.GetType().Name}");

            if (tab == null)
            {
                System.Diagnostics.Debug.WriteLine("ERROR: Tab is null!");
                return;
            }

            if (Tabs == null)
            {
                System.Diagnostics.Debug.WriteLine("ERROR: Tabs collection is null!");
                return;
            }

            // Проверяем, есть ли уже такая вкладка
            var existingTab = Tabs.FirstOrDefault(t => t.GetType() == tab.GetType() && t.Title == tab.Title);
            if (existingTab != null)
            {
                System.Diagnostics.Debug.WriteLine($"Tab already exists, switching to it: {existingTab.Title}");
                SelectedTab = existingTab;
                return;
            }

            // Добавляем новую вкладку
            System.Diagnostics.Debug.WriteLine("Adding new tab to collection...");
            Tabs.Add(tab);
            System.Diagnostics.Debug.WriteLine($"Tabs count after add: {Tabs.Count}");

            // Выбираем новую вкладку
            SelectedTab = tab;
            System.Diagnostics.Debug.WriteLine($"SelectedTab after: {SelectedTab?.GetType().Name}");
            System.Diagnostics.Debug.WriteLine($"SelectedTab title: {SelectedTab?.Title}");

            // Принудительно уведомляем об изменении
            OnPropertyChanged(nameof(Tabs));
            OnPropertyChanged(nameof(SelectedTab));

            System.Diagnostics.Debug.WriteLine("Property changed events fired");
        }

        private void HandleCloseTabMessage(CloseTabMessage message)
        {
            if (message?.TabToClose != null && Tabs.Contains(message.TabToClose))
            {
                Tabs.Remove(message.TabToClose);
                // После закрытия всегда возвращаемся на дашборд
                var dashboard = Tabs.FirstOrDefault(t => t is DashboardViewModel);
                if (dashboard != null)
                {
                    SelectedTab = dashboard;
                }
            }
        }

        private void CloseTab(object tabToClose)
        {
            if (tabToClose is TabViewModelBase tab && Tabs.Contains(tab))
            {
                Tabs.Remove(tab);
            }
        }

        private bool CanCloseTab(object tabToClose)
        {
            // Запрещаем закрывать дашборд
            return tabToClose is TabViewModelBase tab && !(tab is DashboardViewModel);
        }
    }
}
