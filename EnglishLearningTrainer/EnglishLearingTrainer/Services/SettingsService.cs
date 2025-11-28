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
            ApplyAllSettings(); 
        }

        private void ApplyAllSettings()
        {
            // 1. Применяем базовую тему (Словарь)
            string theme = string.IsNullOrEmpty(CurrentSettings.Theme) ? "Light" : CurrentSettings.Theme;
            ThemeService.SetTheme(theme);

            // 2. Накатываем кастомные цвета ПОВЕРХ темы
            if (!string.IsNullOrEmpty(CurrentSettings.BackgroundColor))
                ThemeService.ApplyColor("MainBackgroundBrush", CurrentSettings.BackgroundColor);

            if (!string.IsNullOrEmpty(CurrentSettings.TextColor))
                ThemeService.ApplyColor("PrimaryTextBrush", CurrentSettings.TextColor);

            if (!string.IsNullOrEmpty(CurrentSettings.AccentColor))
                ThemeService.ApplyColor("PrimaryAccentBrush", CurrentSettings.AccentColor);

            // 3. Шрифты
            if (CurrentSettings.BaseFontSize <= 0) CurrentSettings.BaseFontSize = 14;
            Application.Current.Resources["BaseFontSize"] = CurrentSettings.BaseFontSize;
            Application.Current.Resources["HeaderFontSize"] = CurrentSettings.BaseFontSize + 6;
        }

        public void SaveSettings(SettingsModel settings)
        {
            CurrentSettings = settings;
            string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsFilePath, json);

            // Сразу применяем изменения
            ApplyAllSettings();
        }

        public SettingsModel LoadSettings()
        {
            if (File.Exists(_settingsFilePath))
            {
                try
                {
                    string json = File.ReadAllText(_settingsFilePath);
                    return JsonSerializer.Deserialize<SettingsModel>(json) ?? new SettingsModel();
                }
                catch { return new SettingsModel(); }
            }
            return new SettingsModel();
        }
    }
}