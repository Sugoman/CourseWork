using EnglishLearningTrainer.Core;
using EnglishLearningTrainer.Models;
using EnglishLearningTrainer.Services;
using LearningTrainerShared.Models;

namespace EnglishLearningTrainer.ViewModels
{
    public class MainViewModel : ObservableObject
    {
        private object _currentView;
        private IDataService _dataService;
        private IDataService _apiDataService;
        private IDataService _localDataService;
        private readonly SettingsService _settingsService;
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
            _settingsService = new SettingsService();
        }

        public MainViewModel(UserSessionDto savedSession)
        {
            CurrentUser = new User
            {
                Login = savedSession.UserLogin,
                Role = new Role { Name = savedSession.UserRole }
            };

            _apiDataService = new ApiDataService();
            _localDataService = new LocalDataService();

            SyncLocalCacheAsync();

            _apiDataService.SetToken(savedSession.AccessToken);

            var dashboard = new DashboardViewModel(CurrentUser, _apiDataService);
            CurrentView = new ShellViewModel(CurrentUser, _apiDataService, dashboard, new SettingsService());

            EventAggregator.Instance.Subscribe<LogoutRequestedMessage>(HandleLogout);
        }

        private void OpenSettingsTab()
        {
            var settingsVM = new SettingsViewModel(_settingsService);

            EventAggregator.Instance.Publish(settingsVM);
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
            CurrentView = new ShellViewModel(CurrentUser, _apiDataService, dashboard, _settingsService);
        }

        private void OnOfflineLoginRequested()
        {
            CurrentUser = null;
            _apiDataService = null; 
            _localDataService = new LocalDataService();

            var dashboard = new DashboardViewModel(null, _localDataService);
            CurrentView = new ShellViewModel(null, _localDataService, dashboard, _settingsService);
        }

        private void HandleLogout(LogoutRequestedMessage message)
        {
            // Отписываемся от старых сообщений
            EventAggregator.Instance.Unsubscribe<LogoutRequestedMessage>(HandleLogout);

            if (CurrentView is LoginViewModel oldLoginVM)
            {
                oldLoginVM.LoginSuccessful -= OnLoginSuccessful;
                oldLoginVM.OfflineLoginRequested -= OnOfflineLoginRequested;
            }

            SessionService sessionService = new SessionService();
            sessionService.ClearSession();

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
