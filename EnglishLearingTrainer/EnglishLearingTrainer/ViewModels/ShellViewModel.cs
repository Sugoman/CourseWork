using EnglishLearingTrainer.Core;
using EnglishLearningTrainer.Core;
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

        public ObservableCollection<TabViewModelBase> Tabs { get; } = new ObservableCollection<TabViewModelBase>();

        public ICommand CloseTabCommand { get; }

        public ShellViewModel()
        {
            Tabs = new ObservableCollection<TabViewModelBase>();
            CloseTabCommand = new RelayCommand(CloseTab, CanCloseTab);

            EventAggregator.Instance.Subscribe<LearningViewModel>(OpenTab);
            EventAggregator.Instance.Subscribe<CloseTabMessage>(HandleCloseTabMessage);

            var dashboard = new StudentDashboardViewModel();
            Tabs.Add(dashboard);
            SelectedTab = dashboard;
        }

        private void OpenTab(LearningViewModel tab)
        {
            var existingTab = Tabs.FirstOrDefault(t => t is LearningViewModel lvm && lvm.Title == tab.Title);
            if (existingTab == null)
            {
                Tabs.Add(tab);
                SelectedTab = tab;
            }
            else
            {
                SelectedTab = existingTab;
            }
        }

        private void HandleCloseTabMessage(CloseTabMessage message)
        {
            // --- ВОТ ОН, ФИНАЛЬНЫЙ ШТРИХ ---
            // Сначала закрываем вкладку...
            if (message?.TabToClose != null && Tabs.Contains(message.TabToClose))
            {
                Tabs.Remove(message.TabToClose);

                // ...а потом ищем дашборд и делаем его активным!
                var dashboard = Tabs.FirstOrDefault(t => t is StudentDashboardViewModel);
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
            return tabToClose is TabViewModelBase tab && !(tab is StudentDashboardViewModel);
        }
    }
}
