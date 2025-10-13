using EnglishLearingTrainer.Core;
using EnglishLearningTrainer.Core;
using System;
using System.Windows.Controls;
using System.Windows.Input;

namespace EnglishLearningTrainer.ViewModels
{
    public class LoginViewModel : ObservableObject
    {
        public event Action LoginSuccessful;

        private string _username;
        public string Username
        {
            get => _username;
            set
            {
                // Используем прокачанный SetProperty и...
                if (SetProperty(ref _username, value))
                {
                    // ...даем тот самый "пинок" кнопке!
                    (LoginCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        private string _errorMessage;
        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        public ICommand LoginCommand { get; }

        public LoginViewModel()
        {
            LoginCommand = new RelayCommand(Login, CanLogin);
        }

        private bool CanLogin(object parameter)
        {
            // Теперь это условие будет проверяться при каждом изменении Username
            return !string.IsNullOrEmpty(Username);
        }

        private void Login(object parameter)
        {
            var passwordBox = parameter as PasswordBox;
            var password = passwordBox?.Password;

            if (Username == "1" && password == "1")
            {
                ErrorMessage = "";
                LoginSuccessful?.Invoke();
            }
            else
            {
                ErrorMessage = "Неверный логин или пароль";
            }
        }
    }
}