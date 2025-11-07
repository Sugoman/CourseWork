using LearningTrainer.Core;
using LearningTrainer.Services;
using LearningTrainerShared.Models;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

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

        private string _errorMessage;
        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        public RelayCommand LoginCommand { get; }
        public RelayCommand OfflineLoginCommand { get; }

        public LoginViewModel()
        {
            LoginCommand = new RelayCommand(PerformLogin);
            OfflineLoginCommand = new RelayCommand(PerformOfflineLogin);
        }

        private async void PerformLogin(object parameter)
        {

            ErrorMessage = "";

            if (!(parameter is System.Windows.Controls.PasswordBox passwordBox))
            {
                ErrorMessage = "Ошибка получения пароля.";
                return;
            }
            string userPassword = passwordBox.Password;

            var loginRequest = new
            {
                Username = this.Username,
                Password = userPassword
            };
            var jsonOptions = new JsonSerializerOptions
            {
                ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.Preserve,
                PropertyNameCaseInsensitive = true
            };
            try
            {
                HttpResponseMessage response = await _httpClient.PostAsJsonAsync("/api/auth/login", loginRequest);

                if (response.IsSuccessStatusCode)
                {
                    var sessionDto = await response.Content.ReadFromJsonAsync<UserSessionDto>(jsonOptions);

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