using LearningTrainerShared.Models;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System;

namespace LearningTrainer.Services
{
    public class SettingsService
    {
        public event Action<MarkdownConfig> MarkdownConfigChanged;

        public MarkdownConfig CurrentMarkdownConfig => GetConfigFromCurrentTheme();

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

            MarkdownConfigChanged?.Invoke(CurrentMarkdownConfig);
        }

        private MarkdownConfig GetConfigFromCurrentTheme()
        {
            string GetColor(string resourceKey, string fallbackColor)
            {
                if (Application.Current.Resources[resourceKey] is SolidColorBrush brush)
                {
                    return $"#{brush.Color.R:X2}{brush.Color.G:X2}{brush.Color.B:X2}";
                }
                return fallbackColor;
            }

            return new MarkdownConfig
            {
                BackgroundColor = GetColor("SecondaryMainBackgroundBrush", "#FFFFFF"),
                TextColor = GetColor("PrimaryTextBrush", "#000000"),
                AccentColor = GetColor("PrimaryAccentBrush", "#0056b3"),
                FontSize = (int)CurrentSettings.BaseFontSize 
            };
        }

        public void SaveSettings(SettingsModel settings)
        {
            CurrentSettings = settings;
            string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsFilePath, json);

            ApplyFont();
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
            MarkdownConfigChanged?.Invoke(CurrentMarkdownConfig);
        }

        private void ApplyFont()
        {
            if (CurrentSettings.BaseFontSize <= 0) CurrentSettings.BaseFontSize = 14;
            double scale = CurrentSettings.BaseFontSize / 14.0;

            Application.Current.Resources["BaseFontSize"] = CurrentSettings.BaseFontSize;
            Application.Current.Resources["HeaderFontSize"] = CurrentSettings.BaseFontSize + 6;

            Application.Current.Resources["FontSizeSmall"] = Math.Round(12 * scale);
            Application.Current.Resources["FontSizeCaption"] = Math.Round(13 * scale);
            Application.Current.Resources["FontSizeBase"] = Math.Round(14 * scale);
            Application.Current.Resources["FontSizeBody"] = Math.Round(15 * scale);
            Application.Current.Resources["FontSizeMedium"] = Math.Round(16 * scale);
            Application.Current.Resources["FontSizeSubtitle"] = Math.Round(18 * scale);
            Application.Current.Resources["FontSizeLarge"] = Math.Round(20 * scale);
            Application.Current.Resources["FontSizeXLarge"] = Math.Round(22 * scale);
            Application.Current.Resources["FontSizeTitle"] = Math.Round(24 * scale);
            Application.Current.Resources["FontSizeDisplay3"] = Math.Round(28 * scale);
            Application.Current.Resources["FontSizeDisplay2"] = Math.Round(32 * scale);
            Application.Current.Resources["FontSizeDisplay1"] = Math.Round(48 * scale);
            Application.Current.Resources["FontSizeHuge"] = Math.Round(64 * scale);
            Application.Current.Resources["FontSizeMega"] = Math.Round(72 * scale);

            MarkdownConfigChanged?.Invoke(CurrentMarkdownConfig);
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
            return new SettingsModel { Language = "en" };
        }

        private string NormalizeLanguageCode(string input)
        {
            return input switch
            {
                "English" => "en",
                "Русский" => "ru",
                "Español" => "es",
                "Deutsch" => "de",
                "中国人" => "zh",
                _ => input
            };
        }
    }
}
