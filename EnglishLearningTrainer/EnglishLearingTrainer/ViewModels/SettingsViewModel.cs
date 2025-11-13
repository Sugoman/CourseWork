using LearningTrainer.Core;
using LearningTrainer.Services;
using LearningTrainerShared.Models;
using System.Net.Http;
using System.Windows.Input;

namespace LearningTrainer.ViewModels
{
    public class SettingsViewModel : TabViewModelBase
    {
        private readonly SettingsService _settingsService;

        public ICommand LogoutCommand { get; }
        public ICommand ChangePasswordCommand { get; }

        private double _selectedFontSize;
        public double SelectedFontSize
        {
            get => _selectedFontSize;
            set
            {
                if (SetProperty(ref _selectedFontSize, value))
                {
                    ApplyAndSaveSettings();
                }
            }
        }

        private string _oldPassword;
        public string OldPassword
        {
            get => _oldPassword;
            set => SetProperty(ref _oldPassword, value);
        }

        private string _newPassword;
        public string NewPassword
        {
            get => _newPassword;
            set => SetProperty(ref _newPassword, value);
        }

        private string _changePasswordMessage;
        public string ChangePasswordMessage
        {
            get => _changePasswordMessage;
            set => SetProperty(ref _changePasswordMessage, value);
        }

        private bool _isError;
        public bool IsError
        {
            get => _isError;
            set => SetProperty(ref _isError, value);
        }

        private readonly IDataService _dataService;

        public SettingsViewModel(SettingsService settingsService, IDataService dataService)
        {
            Title = "Настройки";
            _settingsService = settingsService;
            _dataService = dataService;

            _selectedFontSize = _settingsService.CurrentSettings.BaseFontSize;
            LogoutCommand = new RelayCommand(PerformLogout);

            ChangePasswordCommand = new RelayCommand(
        async (param) => await ChangePasswordAsync((string)param),
        (param) => !string.IsNullOrWhiteSpace(OldPassword) && !string.IsNullOrWhiteSpace((string)param) // CanExecute
    );
        }

        private void PerformLogout(object obj)
        {
            EventAggregator.Instance.Publish(new LogoutRequestedMessage());
        }

        private void ApplyAndSaveSettings()
        {
            var newSettings = new SettingsModel
            {
                BaseFontSize = this.SelectedFontSize,
                Theme = _settingsService.CurrentSettings.Theme
            };

            _settingsService.ApplySettingsToApp(newSettings);
            _settingsService.SaveSettings(newSettings);
        }
        public async Task ChangePasswordAsync(string newPassword)
        {
            if (string.IsNullOrWhiteSpace(OldPassword) || string.IsNullOrWhiteSpace(newPassword))
            {
                IsError = true;
                ChangePasswordMessage = "Оба поля должны быть заполнены.";
                return;
            }

            try
            {
                var request = new ChangePasswordRequest
                {
                    OldPassword = this.OldPassword,
                    NewPassword = newPassword
                };

                string successMessage = await _dataService.ChangePasswordAsync(request);

                IsError = false;
                ChangePasswordMessage = successMessage; 
            }
            catch (HttpRequestException ex)
            {
                IsError = true;
                ChangePasswordMessage = ex.Message;
            }
            catch (Exception ex)
            {
                IsError = true;
                ChangePasswordMessage = $"Критическая ошибка: {ex.Message}";
            }
        }
    }
}