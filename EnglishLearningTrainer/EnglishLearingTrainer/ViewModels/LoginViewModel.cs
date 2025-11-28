using LearningTrainer.Core;
using LearningTrainer.Services;
using LearningTrainerShared.Models;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Json;

namespace LearningTrainer.ViewModels
{
    public class LoginViewModel : ObservableObject
    {
        private static readonly HttpClient _httpClient = new HttpClient
        {
            BaseAddress = new Uri("http://localhost:5077")
        };
        private readonly SessionService _sessionService = new SessionService();
        private string _username;

        public event Action<UserSessionDto> LoginSuccessful;

        public event Action OfflineLoginRequested;
        public RelayCommand ToggleModeCommand { get; }
        public RelayCommand RegisterCommand { get; }
        public RelayCommand OpenGitHubCommand { get; }

        private readonly IDataService _apiDataService;

        public string Username
        {
            get => _username;
            set
            {
                SetProperty(ref _username, value);
                LoginCommand.RaiseCanExecuteChanged();
                RegisterCommand.RaiseCanExecuteChanged();
            }
        }

        private string _password;
        public string Password
        {
            get => _password;
            set => SetProperty(ref _password, value);
        }

        private bool _isRegisterMode;
        public bool IsRegisterMode
        {
            get => _isRegisterMode;
            set
            {
                SetProperty(ref _isRegisterMode, value);
                LoginCommand.RaiseCanExecuteChanged();
                RegisterCommand.RaiseCanExecuteChanged();
            }
        }

        private string _errorMessage;
        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        private string _currentPassword;
        public string CurrentPassword
        {
            get => _currentPassword;
            set
            {
                SetProperty(ref _currentPassword, value);
                LoginCommand.RaiseCanExecuteChanged();
                RegisterCommand.RaiseCanExecuteChanged();
            }
        }

        private string _confirmPassword;
        public string ConfirmPassword
        {
            get => _confirmPassword;
            set
            {
                SetProperty(ref _confirmPassword, value);
                RegisterCommand.RaiseCanExecuteChanged();
            }
        }

        private string _inviteCode;
        public string InviteCode
        {
            get => _inviteCode;
            set => SetProperty(ref _inviteCode, value);
        }

        public RelayCommand LoginCommand { get; }
        public RelayCommand OfflineLoginCommand { get; }

        public LoginViewModel(SessionService sessionService)
        {
            _apiDataService = new ApiDataService();
            _sessionService = sessionService;
            OfflineLoginCommand = new RelayCommand(PerformOfflineLogin);
            ToggleModeCommand = new RelayCommand(ToggleRegisterMode);
            OpenGitHubCommand = new RelayCommand(PerformOpenGitHub);

            LoginCommand = new RelayCommand(
                async (param) => await PerformLogin(CurrentPassword), 
                (param) => CanLoginExecute()                          
            );
            RegisterCommand = new RelayCommand(
                async (param) => await PerformRegister(ConfirmPassword), 
                (param) => CanRegisterExecute()                          
            );
        }
        private void PerformOpenGitHub(object obj)
        {
            string url = "https://github.com/Sugoman";
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to open URL: {ex.Message}");
                ErrorMessage = "Не удалось открыть ссылку.";
            }
        }
        private bool CanLoginExecute()
        {
            return !IsRegisterMode &&
                   !string.IsNullOrWhiteSpace(Username) &&
                   !string.IsNullOrWhiteSpace(CurrentPassword);
        }

        private bool CanRegisterExecute()
        {
            return IsRegisterMode &&
                   !string.IsNullOrWhiteSpace(Username) &&
                   !string.IsNullOrWhiteSpace(CurrentPassword) &&
                   !string.IsNullOrWhiteSpace(ConfirmPassword);
        }

        private void ToggleRegisterMode(object obj)
        {
            IsRegisterMode = !IsRegisterMode;
            ErrorMessage = "";
        }


        private async Task PerformRegister(string confirmedPassword)
        {
            ErrorMessage = "";
            if (confirmedPassword != CurrentPassword)
            {
                ErrorMessage = "Пароли не совпадают";
                return;
            }

            try
            {
                var request = new RegisterRequest
                {
                    Login = Username,
                    Password = CurrentPassword,
                    InviteCode = string.IsNullOrWhiteSpace(InviteCode) ? null : InviteCode
                };

                string successMessage = await _apiDataService.RegisterAsync(request);

                ErrorMessage = successMessage + ". Теперь можете войти.";
                IsRegisterMode = false;
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
        }

        private async Task PerformLogin(string password)
        {

            ErrorMessage = "";
            var loginRequest = new { Username = this.Username, Password = password };

            try
            {
                try
                {
                    HttpResponseMessage response = await _httpClient.PostAsJsonAsync("/api/auth/login", loginRequest);

                    if (response.IsSuccessStatusCode)
                    {
                        UserSessionDto sessionDto = await _apiDataService.LoginAsync(loginRequest);

                        if (sessionDto == null || string.IsNullOrEmpty(sessionDto.AccessToken))
                        {
                            ErrorMessage = "Ошибка сервера: не получен токен доступа.";
                            return;
                        }

                        _sessionService.SaveSession(sessionDto);

                        _httpClient.DefaultRequestHeaders.Authorization =
                            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", sessionDto.AccessToken);

                        LoginSuccessful?.Invoke(sessionDto);

                    }
                    else
                    {
                        // (401 Unauthorized)
                        var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponseDto>();
                        ErrorMessage = errorResponse.Message;
                    }
                }
                catch (Exception ex)
                {
                    // API не запущен / нет интернета
                    ErrorMessage = "Не удалось подключиться к серверу. Попробуйте автономный режим.";
                    System.Diagnostics.Debug.WriteLine($"API Error: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = "Не удалось подключиться к серверу.";
            }
        }

        private class ErrorResponseDto
        {
            public string Message { get; set; }
        }
        private void PerformOfflineLogin(object obj)
        {
            ErrorMessage = "";
            OfflineLoginRequested?.Invoke();
        }
    }
}