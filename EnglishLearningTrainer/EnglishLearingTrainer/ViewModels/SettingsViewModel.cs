using EnglishLearningTrainer.Core;
using EnglishLearningTrainer.Models;      // <- Убедись, что 'SettingsModel' тут (из 'Shared' проекта)
using EnglishLearningTrainer.Services;    // <- Убедись, что 'SettingsService' тут
using LearningTrainerShared.Models; // (Или где у тебя SettingsModel)
using System.Windows;                 // <- Для 'Application.Current'
using System.Windows.Input;

namespace EnglishLearningTrainer.ViewModels
{
    public class SettingsViewModel : TabViewModelBase
    {
        private readonly SettingsService _settingsService;

        public ICommand LogoutCommand { get; }

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

        public SettingsViewModel(SettingsService settingsService)
        {
            Title = "Настройки";
            _settingsService = settingsService;

            _selectedFontSize = _settingsService.CurrentSettings.BaseFontSize;

            LogoutCommand = new RelayCommand(PerformLogout);
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
    }
}