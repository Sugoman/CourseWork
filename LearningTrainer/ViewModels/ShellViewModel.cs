using LearningTrainer.Core;
using LearningTrainer.Services;
using LearningTrainerShared.Models;
using System.Collections.ObjectModel;
using System.Windows.Input;
using static LearningTrainer.Core.EventAggregator;

namespace LearningTrainer.ViewModels
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
        private readonly AccessNotificationService _notificationService;
        
        public ICommand CloseTabCommand { get; }
        
        // Global notification ViewModel for Shell level
        public AccessNotificationViewModel NotificationViewModel { get; }


        public ShellViewModel(User? user, IDataService dataService, TabViewModelBase initialDashboard, SettingsService settingsService)
        {
            _currentUser = user;
            _dataService = dataService;
            
            // Create global notification service
            _notificationService = new AccessNotificationService();
            NotificationViewModel = new AccessNotificationViewModel(user, _notificationService);

            Tabs = new ObservableCollection<TabViewModelBase>();
            CloseTabCommand = new RelayCommand(CloseTab, CanCloseTab);

            EventAggregator.Instance.Subscribe<LearningViewModel>(OpenTab);
            EventAggregator.Instance.Subscribe<RuleViewModel>(OpenTab);
            EventAggregator.Instance.Subscribe<AddDictionaryViewModel>(OpenTab);
            EventAggregator.Instance.Subscribe<AddRuleViewModel>(OpenTab);
            EventAggregator.Instance.Subscribe<AddWordViewModel>(OpenTab);
            EventAggregator.Instance.Subscribe<CloseTabMessage>(HandleCloseTabMessage);
            EventAggregator.Instance.Subscribe<DictionaryManagementViewModel>(OpenTab);
            EventAggregator.Instance.Subscribe<SettingsViewModel>(OpenTab);
            EventAggregator.Instance.Subscribe<ShareContentViewModel>(OpenTab);
            EventAggregator.Instance.Subscribe<RuleManagementViewModel>(OpenTab);

            // Marketplace subscriptions
            EventAggregator.Instance.Subscribe<MarketplaceViewModel>(OpenTab);
            EventAggregator.Instance.Subscribe<MarketplaceDictionaryDetailsViewModel>(OpenTab);
            EventAggregator.Instance.Subscribe<MarketplaceRuleDetailsViewModel>(OpenTab);

            // Statistics subscription
            EventAggregator.Instance.Subscribe<StatisticsViewModel>(OpenTab);

            // Subscribe to global notifications
            EventAggregator.Instance.Subscribe<ShowNotificationMessage>(OnShowNotification);

            var dashboard = initialDashboard;
            Tabs.Add(dashboard);
            SelectedTab = dashboard;
        }

        private void OnShowNotification(ShowNotificationMessage message)
        {
            switch (message.Type)
            {
                case NotificationType.Success:
                    _notificationService.AddSuccessNotification(message.Title, message.Message);
                    break;
                case NotificationType.Error:
                    _notificationService.AddErrorNotification(message.Title, message.Message);
                    break;
                case NotificationType.Info:
                    _notificationService.AddInfoNotification(message.Title, message.Message);
                    break;
                case NotificationType.Warning:
                    _notificationService.AddInfoNotification(message.Title, message.Message);
                    break;
                case NotificationType.AccessDenied:
                    _notificationService.AddAccessDeniedNotification(message.Title, message.Message, "");
                    break;
            }
        }

        private void OpenTab(TabViewModelBase tab)
        {
            var existingTab = Tabs.FirstOrDefault(t => t.Title == tab.Title);
            if (existingTab == null)

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

            existingTab = Tabs.FirstOrDefault(t => t.GetType() == tab.GetType() && t.Title == tab.Title);
            if (existingTab != null)
            {
                System.Diagnostics.Debug.WriteLine($"Tab already exists, switching to it: {existingTab.Title}");
                SelectedTab = existingTab;
                return;
            }

            System.Diagnostics.Debug.WriteLine("Adding new tab to collection...");
            Tabs.Add(tab);
            System.Diagnostics.Debug.WriteLine($"Tabs count after add: {Tabs.Count}");

            SelectedTab = tab;
            System.Diagnostics.Debug.WriteLine($"SelectedTab after: {SelectedTab?.GetType().Name}");
            System.Diagnostics.Debug.WriteLine($"SelectedTab title: {SelectedTab?.Title}");

            OnPropertyChanged(nameof(Tabs));
            OnPropertyChanged(nameof(SelectedTab));

            System.Diagnostics.Debug.WriteLine("Property changed events fired");
        }

        private void HandleCloseTabMessage(CloseTabMessage message)
        {
            if (message?.TabToClose != null && Tabs.Contains(message.TabToClose))
            {
                Tabs.Remove(message.TabToClose);
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
            return tabToClose is TabViewModelBase tab && !(tab is DashboardViewModel);
        }
    }
}
