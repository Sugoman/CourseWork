using LearningTrainer.Core;
using LearningTrainer.Services;
using LearningTrainerShared.Models;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace LearningTrainer.ViewModels
{
    public class SettingsViewModel : TabViewModelBase
    {
        private readonly SettingsService _settingsService;
        private readonly IDataService _dataService;

        // --- КОМАНДЫ ---
        public ICommand LogoutCommand { get; }
        public ICommand ChangePasswordCommand { get; }
        public ICommand ChangeAccentColorCommand { get; }
        public ICommand UpgradeToTeacherCommand { get; }
        public RelayCommand SwitchSectionCommand { get; }

        // --- СВОЙСТВА НАВИГАЦИИ ---
        private string _currentSection = "General";
        public string CurrentSection
        {
            get => _currentSection;
            set => SetProperty(ref _currentSection, value);
        }

        // --- СВОЙСТВА ТЕМЫ (COLORS) ---
        private string _currentAccentColor;
        public string CurrentAccentColor
        {
            get => _currentAccentColor;
            set
            {
                if (SetProperty(ref _currentAccentColor, value))
                {
                    // 1. Меняем конфиг для WebView (Markdown)
                    UpdateMarkdownConfig();

                    // 2. 🔥 Меняем цвет кнопок во всем приложении
                    ThemeService.ApplyColor("PrimaryAccentBrush", value);
                }
            }
        }

        private string _currentBackgroundColor;
        public string CurrentBackgroundColor
        {
            get => _currentBackgroundColor;
            set
            {
                if (SetProperty(ref _currentBackgroundColor, value))
                {
                    IsDarkMode = _currentBackgroundColor?.ToLower() == "#1e1e1e";
                    UpdateMarkdownConfig();

                    ThemeService.ApplyColor("MainBackgroundBrush", value);

                }
            }
        }

        private string _currentTextColor;
        public string CurrentTextColor
        {
            get => _currentTextColor;
            set
            {
                if (SetProperty(ref _currentTextColor, value))
                {
                    UpdateMarkdownConfig();

                    // 4. 🔥 Меняем цвет текста
                    ThemeService.ApplyColor("PrimaryTextBrush", value);
                }
            }
        }

        private double _selectedFontSize;
        public double SelectedFontSize
        {
            get => _selectedFontSize;
            set
            {
                if (SetProperty(ref _selectedFontSize, value))
                {
                    ApplyAndSaveAppThemeSettings();
                    UpdateMarkdownConfig();
                }
            }
        }
        private string _appAccentColor;
        public string AppAccentColor
        {
            get => _appAccentColor;
            set
            {
                if (SetProperty(ref _appAccentColor, value))
                {
                    ThemeService.ApplyColor("PrimaryAccentBrush", value);

                    ApplyAndSaveAppThemeSettings();
                }
            }
        }

        private string _appBackgroundColor;
        public string AppBackgroundColor
        {
            get => _appBackgroundColor;
            set
            {
                if (SetProperty(ref _appBackgroundColor, value))
                {
                    ThemeService.ApplyColor("MainBackgroundBrush", value);

                    ApplyAndSaveAppThemeSettings();
                }
            }
        }

        private string _appTextColor;
        public string AppTextColor
        {
            get => _appTextColor;
            set
            {
                if (SetProperty(ref _appTextColor, value))
                {
                    ThemeService.ApplyColor("PrimaryTextBrush", value);
                    ApplyAndSaveAppThemeSettings();
                }
            }
        }
        // Тумблер темной темы
        private bool _isDarkMode;
        public bool IsDarkMode
        {
            get => _isDarkMode;
            set
            {
                if (SetProperty(ref _isDarkMode, value))
                {
                    ApplyThemePreset(value);
                }
            }
        }

        private MarkdownConfig _currentMarkdownConfig;
        public MarkdownConfig CurrentMarkdownConfig
        {
            get => _currentMarkdownConfig;
            private set => SetProperty(ref _currentMarkdownConfig, value);
        }

        // --- СВОЙСТВА УЧИТЕЛЯ ---
        private string _teacherCode;
        public string TeacherCode
        {
            get => _teacherCode;
            set => SetProperty(ref _teacherCode, value);
        }

        // --- СВОЙСТВА АККАУНТА ---
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

        private int _dailyGoal = 20;
        public int DailyGoal
        {
            get => _dailyGoal;
            set => SetProperty(ref _dailyGoal, value);
        }

        // --- СВОЙСТВА ЛОКАЛИЗАЦИИ ---
        public List<string> AvailableLanguages { get; } = new List<string> { "English", "Русский", "Español", "Deutsch" };

        private string _currentLanguage = "English";
        public string CurrentLanguage
        {
            get => _currentLanguage;
            set
            {
                if (SetProperty(ref _currentLanguage, value))
                {
                    ChangeLanguage(value);
                }
            }
        }

        // ============================================================
        // КОНСТРУКТОР
        // ============================================================
        public SettingsViewModel(SettingsService settingsService, IDataService dataService, User currentUser)
        {
            Title = "Настройки";
            _settingsService = settingsService;
            _dataService = dataService;

            if (currentUser != null)
            {
                TeacherCode = currentUser.InviteCode;
            }

            /*var savedConfig = _settingsService.CurrentMarkdownConfig;
            _selectedFontSize = savedConfig.FontSize; 
            _currentAccentColor = savedConfig.AccentColor;
            _currentBackgroundColor = savedConfig.BackgroundColor;
            _currentTextColor = savedConfig.TextColor;
            _currentMarkdownConfig = savedConfig;*/

            _isDarkMode = _currentBackgroundColor?.ToLower() == "#1e1e1e";

            LogoutCommand = new RelayCommand(PerformLogout);
            SwitchSectionCommand = new RelayCommand(sec => CurrentSection = (string)sec);

            UpgradeToTeacherCommand = new RelayCommand(async (_) => await PerformUpgradeToTeacher());

            ChangePasswordCommand = new RelayCommand(
                async (param) => await ChangePasswordAsync((string)param),
                (param) => !string.IsNullOrWhiteSpace(OldPassword) && !string.IsNullOrWhiteSpace((string)param)
            );

            ChangeAccentColorCommand = new RelayCommand(hexColor =>
            {
                CurrentAccentColor = (string)hexColor; // Триггерит сеттер -> UpdateMarkdownConfig
            });

            AppAccentColor = _settingsService.CurrentSettings.AccentColor;
            AppBackgroundColor = _settingsService.CurrentSettings.BackgroundColor;
            AppTextColor = _settingsService.CurrentSettings.TextColor;

        }

        // ============================================================
        // МЕТОДЫ ЛОГИКИ
        // ============================================================

        private void ApplyThemePreset(bool isDark)
        {
            ThemeService.SetTheme(isDark ? "Dark" : "Light");

            if (isDark)
            {
                CurrentBackgroundColor = "#1E1E1E";
                CurrentTextColor = "#E5E7EB";

                ThemeService.ApplyColor("CardBackgroundBrush", "#1E1E1E");
                ThemeService.ApplyColor("CardPartBackgroundBrush", "#1A1A1A");
            }
            else
            {
                CurrentBackgroundColor = "#FFFFFF";
                CurrentTextColor = "#000000";

                ThemeService.ApplyColor("CardBackgroundBrush", "#FFFFFF");
                ThemeService.ApplyColor("CardPartBackgroundBrush", "#F1F1F1");
            }
        }

        private void UpdateMarkdownConfig()
        {
            var newConfig = new MarkdownConfig
            {
                FontSize = (int)SelectedFontSize,
                AccentColor = CurrentAccentColor,
                BackgroundColor = CurrentBackgroundColor,
                TextColor = CurrentTextColor
            };

            CurrentMarkdownConfig = newConfig;

            //_settingsService.UpdateMarkdownAppearance(newConfig);
        }

        private void ApplyAndSaveAppThemeSettings()
        {
            var newSettings = new SettingsModel
            {
                BaseFontSize = this.SelectedFontSize,

                AccentColor = AppAccentColor,
                BackgroundColor = AppBackgroundColor,
                TextColor = AppTextColor,

                Theme = IsDarkMode ? "Dark" : "Light"
            };

            //_settingsService.ApplySettingsToApp(newSettings);
            _settingsService.SaveSettings(newSettings);
        }

        private void PerformLogout(object obj)
        {
            EventAggregator.Instance.Publish(new LogoutRequestedMessage());
        }

        private async Task PerformUpgradeToTeacher()
        {
            try
            {
                var result = await _dataService.UpgradeToTeacherAsync();
                TeacherCode = result.InviteCode;

                MessageBox.Show($"Поздравляем! Вы стали учителем.\nВаш код приглашения: {result.InviteCode}",
                                "Статус обновлен", MessageBoxButton.OK, MessageBoxImage.Information);

                EventAggregator.Instance.Publish(new RoleChangedMessage { NewToken = result.AccessToken, NewRole = result.UserRole });
            }
            catch (HttpRequestException ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Не удалось обновить статус", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public async Task ChangePasswordAsync(string newPassword)
        {
            if (string.IsNullOrWhiteSpace(OldPassword) || string.IsNullOrWhiteSpace(newPassword))
            {
                IsError = true;
                ChangePasswordMessage = "Заполните все поля.";
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
                OldPassword = "";
                NewPassword = "";
            }
            catch (HttpRequestException ex)
            {
                IsError = true;
                ChangePasswordMessage = ex.Message;
            }
            catch (Exception ex)
            {
                IsError = true;
                ChangePasswordMessage = $"Ошибка: {ex.Message}";
            }
        }

        private void ChangeLanguage(string languageName)
        {
            string code = languageName switch
            {
                "Русский" => "ru",
                "English" => "en",
                _ => "en"
            };
            LocalizationManager.SetLanguage(code);
        }
    }
}