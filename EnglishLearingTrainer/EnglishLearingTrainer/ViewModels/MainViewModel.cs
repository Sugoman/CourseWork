using EnglishLearningTrainer.Core;
using EnglishLearningTrainer.Models;
using EnglishLearningTrainer.Services;

namespace EnglishLearningTrainer.ViewModels
{
    public class MainViewModel : ObservableObject
    {
        private object _currentView;
        private IDataService _dataService;
        private IDataService _apiDataService;
        private IDataService _localDataService;
        public User? CurrentUser { get; private set; }

        public object CurrentView
        {
            get { return _currentView; }
            set
            {
                _currentView = value;
                OnPropertyChanged();
            }
        }

        public MainViewModel()
        {
            ShowLoginView();
        }

        private void ShowLoginView()
        {
            var loginVM = new LoginViewModel();
            loginVM.LoginSuccessful += OnLoginSuccessful;
            loginVM.OfflineLoginRequested += OnOfflineLoginRequested;

            EventAggregator.Instance.Subscribe<LogoutRequestedMessage>(HandleLogout);

            CurrentView = loginVM;
        }

        private void OnLoginSuccessful(User loggedInUser)
        {
            CurrentUser = loggedInUser;
            _apiDataService = new ApiDataService();
            _localDataService = new LocalDataService();

            SyncLocalCacheAsync();

            var dashboard = new DashboardViewModel(CurrentUser, _apiDataService);
            CurrentView = new ShellViewModel(CurrentUser, _apiDataService, dashboard);
        }

        private void OnOfflineLoginRequested()
        {
            CurrentUser = null;
            _apiDataService = null; 
            _localDataService = new LocalDataService();

            var dashboard = new DashboardViewModel(null, _localDataService);
            CurrentView = new ShellViewModel(null, _localDataService, dashboard);
        }

        private void HandleLogout(LogoutRequestedMessage message)
        {
            EventAggregator.Instance.Unsubscribe<LogoutRequestedMessage>(HandleLogout);
      
            if (CurrentView is LoginViewModel oldLoginVM)
            {
                oldLoginVM.LoginSuccessful -= OnLoginSuccessful;
                oldLoginVM.OfflineLoginRequested -= OnOfflineLoginRequested;
            }

            CurrentUser = null;
            _apiDataService = null;
            _localDataService = null;

            ShowLoginView();
        }

        private async Task SyncLocalCacheAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine(">>> НАЧАЛО СИНХРОНИЗАЦИИ ЛОКАЛЬНОГО КЭША...");

                var dictionaries = await _apiDataService.GetDictionariesAsync();
                var rules = await _apiDataService.GetRulesAsync();

                await _localDataService.WipeAndStoreDictionariesAsync(dictionaries);
                await _localDataService.WipeAndStoreRulesAsync(rules);

                System.Diagnostics.Debug.WriteLine(">>> СИНХРОНИЗАЦИЯ УСПЕШНО ЗАВЕРШЕНА.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"!!! ОШИБКА СИНХРОНИЗАЦИИ: {ex.Message}");
            }
        }
    }

}
