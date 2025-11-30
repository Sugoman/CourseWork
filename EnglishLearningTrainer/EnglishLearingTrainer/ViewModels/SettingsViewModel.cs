using LearningTrainer.Core;
using LearningTrainer.Services;
using LearningTrainerShared.Models;
using System.Collections.Generic;
using System.Net.Http;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Linq;
using System;

namespace LearningTrainer.ViewModels
{
    public class SettingsViewModel : TabViewModelBase
    {
        private readonly SettingsService _settingsService;
        private readonly IDataService _dataService;

        // --- КОМАНДЫ ---
        public ICommand LogoutCommand { get; }
        public ICommand ChangePasswordCommand { get; }
        public ICommand UpgradeToTeacherCommand { get; }
        public RelayCommand SwitchSectionCommand { get; }

        // --- СВОЙСТВА НАВИГАЦИИ ---
        private string _currentSection = "General";
        public string CurrentSection
        {
            get => _currentSection;
            set => SetProperty(ref _currentSection, value);
        }

        // --- ЯЗЫКИ ---
        private readonly Dictionary<string, string> _languagesMap = new()
        {
            { "English", "en" },
            { "Русский", "ru" },
            { "Español", "es" },
            { "Deutsch", "de" },
            { "中国人", "zh" }
        };
        public List<string> AvailableLanguages => _languagesMap.Keys.ToList();

        private string _currentLanguage;
        public string CurrentLanguage
        {
            get => _currentLanguage;
            set
            {
                if (SetProperty(ref _currentLanguage, value))
                {
                    if (_languagesMap.TryGetValue(value, out string code))
                    {
                        _settingsService.ApplyLanguage(code);
                    }
                }
            }
        }

        // --- ТЕМЫ ---
        public List<string> AvailableThemes { get; } = new List<string> { "Light", "Dark", "Dracula", "Forest" };

        private string _selectedTheme;
        public string SelectedTheme
        {
            get => _selectedTheme;
            set
            {
                if (SetProperty(ref _selectedTheme, value))
                {
                    _settingsService.ApplyTheme(value);
                    UpdateColorsFromResources(); // Обновляем цвета в UI
                }
            }
        }

        // --- ШРИФТ ---
        private double _selectedFontSize;
        public double SelectedFontSize
        {
            get => _selectedFontSize;
            set
            {
                if (SetProperty(ref _selectedFontSize, value))
                {
                    // Обновляем напрямую в сервисе
                    _settingsService.CurrentSettings.BaseFontSize = value;
                    _settingsService.SaveSettings(_settingsService.CurrentSettings);

                    // Тут можно вызвать применение шрифта, если оно не автоматическое
                    // Но обычно оно через ресурсы работает
                }
            }
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

        // --- ЦВЕТА (Только для чтения в UI, менять их лучше через тему) ---
        private string _appBackgroundColor;
        public string AppBackgroundColor
        {
            get => _appBackgroundColor;
            set => SetProperty(ref _appBackgroundColor, value);
        }

        private string _appTextColor;
        public string AppTextColor
        {
            get => _appTextColor;
            set => SetProperty(ref _appTextColor, value);
        }

        private string _appAccentColor;
        public string AppAccentColor
        {
            get => _appAccentColor;
            set => SetProperty(ref _appAccentColor, value);
        }

        // ============================================================
        // КОНСТРУКТОР
        // ============================================================
        public SettingsViewModel(SettingsService settingsService, IDataService dataService, User currentUser)
        {
            Title = "Settings";
            _settingsService = settingsService;
            _dataService = dataService;

            if (currentUser != null)
            {
                TeacherCode = currentUser.InviteCode;
            }

            // --- ИНИЦИАЛИЗАЦИЯ ПОЛЕЙ (БЕЗ ВЫЗОВА СЕТТЕРОВ) ---
            // Используем приватные поля (_field), чтобы НЕ срабатывала логика сохранения при открытии окна

            _selectedTheme = _settingsService.CurrentSettings.Theme;
            _selectedFontSize = _settingsService.CurrentSettings.BaseFontSize;

            // Инициализация языка
            string currentCode = _settingsService.CurrentSettings.Language;
            _currentLanguage = _languagesMap.FirstOrDefault(x => x.Value == currentCode).Key ?? "English";

            // Команды
            LogoutCommand = new RelayCommand(PerformLogout);
            SwitchSectionCommand = new RelayCommand(sec => CurrentSection = (string)sec);
            UpgradeToTeacherCommand = new RelayCommand(async (_) => await PerformUpgradeToTeacher());

            ChangePasswordCommand = new RelayCommand(
                async (param) => await ChangePasswordAsync((string)param),
                (param) => !string.IsNullOrWhiteSpace(OldPassword) && !string.IsNullOrWhiteSpace((string)param)
            );

            // Обновляем квадратики с цветами
            UpdateColorsFromResources();
        }

        // ============================================================
        // МЕТОДЫ ЛОГИКИ
        // ============================================================

        private void PerformLogout(object obj)
        {
            EventAggregator.Instance.Publish(new LogoutRequestedMessage());
        }

        private async Task PerformUpgradeToTeacher()
        {
            try
            {
                if (_dataService.UpgradeToTeacherAsync() == null)
                {
                    throw new InvalidOperationException("Невозможно использовать онлайн функции в офлайн режиме");
                }
                else
                {
                    var result = await _dataService.UpgradeToTeacherAsync();
                    TeacherCode = result.InviteCode;

                    MessageBox.Show($"Поздравляем! Вы стали учителем.\nВаш код приглашения: {result.InviteCode}",
                                    "Статус обновлен", MessageBoxButton.OK, MessageBoxImage.Information);

                    EventAggregator.Instance.Publish(new RoleChangedMessage { NewToken = result.AccessToken, NewRole = result.UserRole });
                }
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

        private void UpdateColorsFromResources()
        {
            string GetHex(string key)
            {
                if (Application.Current.Resources[key] is SolidColorBrush brush)
                {
                    return brush.Color.ToString();
                }
                return "#000000";
            }

            AppBackgroundColor = GetHex("MainBackgroundBrush");
            AppTextColor = GetHex("PrimaryTextBrush");
            AppAccentColor = GetHex("PrimaryAccentBrush");
        }
    }
}