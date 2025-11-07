using LearningTrainer.Core;
using LearningTrainer.Services;    
using LearningTrainerShared.Models;
using System.Windows.Input;

namespace LearningTrainer.ViewModels
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