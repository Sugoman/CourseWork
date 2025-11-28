using LearningTrainer.Core;
using LearningTrainer.Services;
using LearningTrainerShared.Models;
using System.Net.Http;
using System.Net.Http.Headers;

namespace LearningTrainer.ViewModels
{
    public class MainViewModel : ObservableObject
    {
        private object _currentView;
        private IDataService _dataService;
        private IDataService _apiDataService;
        private IDataService _localDataService;
        private readonly SettingsService _settingsService;
        private readonly SessionService _sessionService;
        public MarkdownConfig CurrentMarkdownConfig { get; private set; } = new MarkdownConfig();
        public event Action<MarkdownConfig> MarkdownConfigChanged;
        MarkdownConfig markdownConfig;
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
            _sessionService = new SessionService(); 
            ShowLoginView();
        }

        public MainViewModel(UserSessionDto savedSession)
        {
            CurrentUser = new User
            {
                Id = savedSession.UserId,
                Login = savedSession.UserLogin,
                Role = new Role { Name = savedSession.UserRole },
                InviteCode = savedSession.InviteCode
            };
            _settingsService = new SettingsService();
            _sessionService = new SessionService();
            _apiDataService = new ApiDataService();
            _localDataService = new LocalDataService(CurrentUser.Login);


            _apiDataService.SetToken(savedSession.AccessToken);

            EventAggregator.Instance.Subscribe<LogoutRequestedMessage>(HandleLogout);
            SyncLocalCacheAsync();
            InitializeSessionAsync();
        }
        private async void InitializeSessionAsync()
        {
            bool syncSuccess = await SyncLocalCacheAsync();

            if (syncSuccess)
            {
                var dashboard = new DashboardViewModel(CurrentUser, _apiDataService, _settingsService);
                CurrentView = new ShellViewModel(CurrentUser, _apiDataService, dashboard, _settingsService);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine(">>> ОШИБКА ИНИЦИАЛИЗАЦИИ. Принудительный выход...");
                HandleLogout(new LogoutRequestedMessage());
            }
        }
        private void OpenSettingsTab()
        {
            var settingsVM = new SettingsViewModel(_settingsService, _dataService, CurrentUser);
            EventAggregator.Instance.Publish(settingsVM);
        }
        private void ShowLoginView()
        {
            var loginVM = new LoginViewModel(_sessionService);
            loginVM.LoginSuccessful += OnLoginSuccessful;
            loginVM.OfflineLoginRequested += OnOfflineLoginRequested;

            EventAggregator.Instance.Subscribe<LogoutRequestedMessage>(HandleLogout);

            CurrentView = loginVM;
        }

        private void OnLoginSuccessful(UserSessionDto sessionDto)
        {
            System.Diagnostics.Debug.WriteLine($"Login Success! UserID: {sessionDto.UserId}");
            CurrentUser = new User
            {
                Id = sessionDto.UserId,
                Login = sessionDto.UserLogin,
                Role = new Role { Name = sessionDto.UserRole },
                InviteCode = sessionDto.InviteCode
            };

            _apiDataService = new ApiDataService();
            _localDataService = new LocalDataService(CurrentUser.Login);

            _apiDataService.SetToken(sessionDto.AccessToken);


            var dashboard = new DashboardViewModel(CurrentUser, _apiDataService, _settingsService);
            CurrentView = new ShellViewModel(CurrentUser, _apiDataService, dashboard, _settingsService);
            SyncLocalCacheAsync();
        }

        private void OnOfflineLoginRequested()
        {
            var savedSession = _sessionService.LoadSession();
            string offlineLogin = savedSession?.UserLogin;

            if (string.IsNullOrEmpty(offlineLogin))
            {
                offlineLogin = _sessionService.LoadLastUserLogin();
            }

            CurrentUser = null;
            _apiDataService = null;

            _localDataService = new LocalDataService(offlineLogin);

            var dashboard = new DashboardViewModel(CurrentUser, _localDataService, _settingsService);
            CurrentView = new ShellViewModel(CurrentUser, _localDataService, dashboard, _settingsService);
        }

        private void HandleLogout(LogoutRequestedMessage message)
        {
            EventAggregator.Instance.Unsubscribe<LogoutRequestedMessage>(HandleLogout);

            if (CurrentView is LoginViewModel oldLoginVM)
            {
                oldLoginVM.LoginSuccessful -= OnLoginSuccessful;
                oldLoginVM.OfflineLoginRequested -= OnOfflineLoginRequested;
            }

            _sessionService.ClearSession();

            CurrentUser = null;
            _apiDataService = null;
            _localDataService = null;

            ShowLoginView();
        }

        private async Task<bool> SyncLocalCacheAsync() 
        {
            try
            {
                System.Diagnostics.Debug.WriteLine(">>> НАЧАЛО СИНХРОНИЗАЦИИ ЛОКАЛЬНОГО КЭША...");

                var dictionaries = await _apiDataService.GetDictionariesAsync();
                var rules = await _apiDataService.GetRulesAsync();

                await _localDataService.WipeAndStoreDictionariesAsync(dictionaries);
                await _localDataService.WipeAndStoreRulesAsync(rules);

                System.Diagnostics.Debug.WriteLine(">>> СИНХРОНИЗАЦИЯ УСПЕШНО ЗАВЕРШЕНА.");
                return true; 
            }
            catch (HttpRequestException httpEx) when (httpEx.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                if (httpEx.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    System.Diagnostics.Debug.WriteLine($"!!! ОШИБКА 401: Токен истек! (HTTP)");
                    return false; 
                }

                System.Diagnostics.Debug.WriteLine($"!!! ОШИБКА HTTP: {httpEx.Message}");
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"!!! ОШИБКА СИНХРОНИЗАЦИИ: {ex.Message}");

                if (ex.Message.Contains("401"))
                {
                    System.Diagnostics.Debug.WriteLine($"!!! ОШИБКА 401: Токен истек! (Exception)");
                    return false;
                }
                return false;
            }
        }
    }

}
