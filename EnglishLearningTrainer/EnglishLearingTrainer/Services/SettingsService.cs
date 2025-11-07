using LearningTrainerShared.Models;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace LearningTrainer.Services
{
    public class SettingsService
    {
        private readonly string _settingsFilePath = "settings.json";
        public SettingsModel CurrentSettings { get; private set; }

        public SettingsService()
        {
            CurrentSettings = LoadSettings();
            ApplySettingsToApp(CurrentSettings); 
        }

        public SettingsModel LoadSettings()
        {
            if (File.Exists(_settingsFilePath))
            {
                string json = File.ReadAllText(_settingsFilePath);
                return JsonSerializer.Deserialize<SettingsModel>(json) ?? new SettingsModel();
            }
            return new SettingsModel();
        }

        public void SaveSettings(SettingsModel settings)
        {
            CurrentSettings = settings;
            string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsFilePath, json);
        }

        public void ApplySettingsToApp(SettingsModel settings)
        {
            Application.Current.Resources["BaseFontSize"] = settings.BaseFontSize;
            Application.Current.Resources["HeaderFontSize"] = settings.BaseFontSize + 6;
        }
    }
}