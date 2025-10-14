using EnglishLearningTrainer.Core;
using EnglishLearningTrainer.Models;
using EnglishLearningTrainer.Services;

namespace EnglishLearningTrainer.ViewModels
{
    // Главная ViewModel, которая управляет навигацией
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
            // Начинаем с экрана входа
            ShowLoginView();
        }

        // --- ВЫНЕСИ ЛОГИКУ СОЗДАНИЯ LoginView В ОТДЕЛЬНЫЙ МЕТОД ---
        private void ShowLoginView()
        {
            var loginVM = new LoginViewModel();
            loginVM.LoginSuccessful += OnLoginSuccessful;
            loginVM.OfflineLoginRequested += OnOfflineLoginRequested;

            // --- И ДОБАВЬ ПОДПИСКУ НА ВЫХОД ---
            EventAggregator.Instance.Subscribe<LogoutRequestedMessage>(HandleLogout);
            // ---------------------------------

            CurrentView = loginVM;
        }

        private void OnLoginSuccessful(User loggedInUser)
        {
            CurrentUser = loggedInUser;
            _apiDataService = new ApiDataService();
            _localDataService = new LocalDataService();

            SyncLocalCacheAsync(); // Запускаем синхронизацию

            var dashboard = new DashboardViewModel(CurrentUser, _apiDataService);
            CurrentView = new ShellViewModel(CurrentUser, _apiDataService, dashboard);
        }

        private void OnOfflineLoginRequested()
        {
            CurrentUser = null;
            _apiDataService = null; // В офлайне он не нужен
            _localDataService = new LocalDataService(); // Создаем только локальный

            var dashboard = new DashboardViewModel(null, _localDataService);
            CurrentView = new ShellViewModel(null, _localDataService, dashboard);
        }

        // --- ДОБАВЬ ЭТОТ МЕТОД ---
        // В MainViewModel.cs
        private void HandleLogout(LogoutRequestedMessage message)
        {
            // --- ВОТ ОН, ФИКС ---
            // Отписываемся от ЭТОГО сообщения, чтобы не ловить его снова
            EventAggregator.Instance.Unsubscribe<LogoutRequestedMessage>(HandleLogout);
            // ------------------

            // Отписываемся и от сообщений LoginViewModel, если он еще жив
            if (CurrentView is LoginViewModel oldLoginVM)
            {
                oldLoginVM.LoginSuccessful -= OnLoginSuccessful;
                oldLoginVM.OfflineLoginRequested -= OnOfflineLoginRequested;
            }

            // Сбрасываем всё
            CurrentUser = null;
            _apiDataService = null;
            _localDataService = null;

            // Показываем экран входа ЗАНОВО
            ShowLoginView(); // Этот метод теперь заново подпишется на всё
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
                // (Если упало - не страшно, юзер всё равно в онлайне)
                System.Diagnostics.Debug.WriteLine($"!!! ОШИБКА СИНХРОНИЗАЦИИ: {ex.Message}");
            }
        }
    }

}
