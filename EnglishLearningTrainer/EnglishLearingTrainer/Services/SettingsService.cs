using LearningTrainerShared.Models;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;

namespace LearningTrainer.Services
{
    public class SettingsService
    {
        public event Action<MarkdownConfig> MarkdownConfigChanged;
        private bool _isDarkTheme = false;
        public MarkdownConfig CurrentMarkdownConfig => _isDarkTheme ? DarkConfig : LightConfig;
        private readonly string _settingsFilePath = "settings.json";
        public SettingsModel CurrentSettings { get; private set; }

        public SettingsService()
        {
            CurrentSettings = LoadSettings();
            string langCode = NormalizeLanguageCode(CurrentSettings.Language);
            if (CurrentSettings.Language != langCode)
            {
                CurrentSettings.Language = langCode;
                SaveSettings(CurrentSettings);
            }
            string savedTheme = string.IsNullOrEmpty(CurrentSettings.Theme) ? "Light" : CurrentSettings.Theme;
            ThemeService.SetTheme(savedTheme);
            ApplyFont();
            ApplyLanguage(langCode);
        }

        private MarkdownConfig DarkConfig => new MarkdownConfig
        {
            BackgroundColor = "#131314", // MainBackgroundBrush
            TextColor = "#E5E7EB",       // PrimaryTextBrush
            AccentColor = "#60A5FA",     // PrimaryAccentBrush
            FontSize = 16
        };

        private MarkdownConfig LightConfig => new MarkdownConfig
        {
            BackgroundColor = "#FFFFFF",
            TextColor = "#333333",
            AccentColor = "#0056b3",
            FontSize = 16
        };

        public void SaveSettings(SettingsModel settings)
        {
            CurrentSettings = settings;
            string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsFilePath, json);

            ApplyFont();
        }
        public void ApplyCustomColors(bool reloadBaseTheme = false)
        {
            if (reloadBaseTheme)
            {
                string currentTheme = string.IsNullOrEmpty(CurrentSettings.Theme) ? "Light" : CurrentSettings.Theme;
                ThemeService.SetTheme(currentTheme);
            }

        }
        public void ApplyLanguage(string languageCode)
        {
            CurrentSettings.Language = languageCode;
            SaveSettings(CurrentSettings);

            LanguageService.SetLanguage(languageCode);

        }
        

        public void ApplyTheme(string themeName)
        {
            CurrentSettings.Theme = themeName;
            SaveSettings(CurrentSettings);

            ThemeService.SetTheme(themeName);
        }

        private void ApplyFont()
        {
            if (CurrentSettings.BaseFontSize <= 0) CurrentSettings.BaseFontSize = 14;
            Application.Current.Resources["BaseFontSize"] = CurrentSettings.BaseFontSize;
            Application.Current.Resources["HeaderFontSize"] = CurrentSettings.BaseFontSize + 6;
        }

        private SettingsModel LoadSettings()
        {
            if (File.Exists(_settingsFilePath))
            {
                try
                {
                    string json = File.ReadAllText(_settingsFilePath);
                    return JsonSerializer.Deserialize<SettingsModel>(json) ?? new SettingsModel();
                }
                catch { }
            }
            return new SettingsModel();
        }
        public void SetTheme(bool isDark)
        {
            _isDarkTheme = isDark;

            MarkdownConfigChanged?.Invoke(CurrentMarkdownConfig);
        }

        private string NormalizeLanguageCode(string input)
        {
            return input switch
            {
                "English" => "en",
                "Русский" => "ru",
                "Español" => "es",
                "Deutsch" => "de",
                "中国人" => "ch", 
                _ => input
            };
        }

    }
}