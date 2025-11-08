using LearningTrainer.Core;
using LearningTrainer.Services;
using LearningTrainerShared.Models;
using System.Net.Http;
using System.Net.Http.Json;

namespace LearningTrainer.ViewModels
{
    public class LoginViewModel : ObservableObject
    {
        private static readonly HttpClient _httpClient = new HttpClient
        {
            BaseAddress = new Uri("http://localhost:5076")
        };
        private readonly SessionService _sessionService = new SessionService();
        private string _username;

        public event Action<UserSessionDto> LoginSuccessful;

        public event Action OfflineLoginRequested;
        public RelayCommand ToggleModeCommand { get; }
        public RelayCommand SubmitCommand { get; }

        private readonly IDataService _apiDataService;

        public string Username
        {
            get => _username;
            set => SetProperty(ref _username, value);
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
            set => SetProperty(ref _isRegisterMode, value);
        }

        private string _errorMessage;
        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        public RelayCommand LoginCommand { get; }
        public RelayCommand OfflineLoginCommand { get; }

        public LoginViewModel(SessionService sessionService)
        {
            _apiDataService = new ApiDataService();
            _sessionService = sessionService;
            OfflineLoginCommand = new RelayCommand(PerformOfflineLogin);
            ToggleModeCommand = new RelayCommand(ToggleRegisterMode);

            SubmitCommand = new RelayCommand((_) => { });
        }



        private void ToggleRegisterMode(object obj)
        {
            IsRegisterMode = !IsRegisterMode;
            ErrorMessage = "";
        }

        public async Task PerformSubmit(string password, string confirmPassword)
        {
            if (IsRegisterMode)
            {
                await PerformRegister(password, confirmPassword);
            }
            else
            {
                await PerformLogin(password);
            }
        }

        private async Task PerformRegister(string password, string confirmPassword)
        {
            ErrorMessage = "";
            if (password != confirmPassword)
            {
                ErrorMessage = "Пароли не совпадают";
                return;
            }

            try
            {
                var request = new RegisterRequest { Login = Username, Password = password };
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