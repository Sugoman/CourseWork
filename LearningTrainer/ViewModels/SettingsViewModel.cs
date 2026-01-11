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
        public ICommand CopyTeacherCodeCommand { get; }
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
                    UpdateColorsFromResources();
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
                    _settingsService.CurrentSettings.BaseFontSize = value;
                    _settingsService.SaveSettings(_settingsService.CurrentSettings);
                }
            }
        }

        // --- ШРИФТЫ (СЕМЕЙСТВО) ---
        public List<string> AvailableFontFamilies { get; } = new List<string> 
        { 
            "Segoe UI", 
            "Arial", 
            "Consolas", 
            "Calibri", 
            "Times New Roman" 
        };

        private string _selectedFontFamily;
        public string SelectedFontFamily
        {
            get => _selectedFontFamily;
            set
            {
                if (SetProperty(ref _selectedFontFamily, value))
                {
                    _settingsService.CurrentSettings.FontFamily = value;
                    _settingsService.SaveSettings(_settingsService.CurrentSettings);
                    Application.Current.Resources["BaseFontFamily"] = new System.Windows.Media.FontFamily(value);
                }
            }
        }

        // --- АНИМАЦИИ ---
        private bool _enableAnimations;
        public bool EnableAnimations
        {
            get => _enableAnimations;
            set
            {
                if (SetProperty(ref _enableAnimations, value))
                {
                    _settingsService.CurrentSettings.EnableAnimations = value;
                    _settingsService.SaveSettings(_settingsService.CurrentSettings);
                }
            }
        }

        // --- УВЕДОМЛЕНИЯ ---
        private bool _enableNotifications;
        public bool EnableNotifications
        {
            get => _enableNotifications;
            set
            {
                if (SetProperty(ref _enableNotifications, value))
                {
                    _settingsService.CurrentSettings.EnableNotifications = value;
                    _settingsService.SaveSettings(_settingsService.CurrentSettings);
                }
            }
        }

        private int _notificationDuration;
        public int NotificationDuration
        {
            get => _notificationDuration;
            set
            {
                if (SetProperty(ref _notificationDuration, value))
                {
                    _settingsService.CurrentSettings.NotificationDurationSeconds = value;
                    _settingsService.SaveSettings(_settingsService.CurrentSettings);
                }
            }
        }

        // --- ОБУЧЕНИЕ ---
        private int _dailyGoal;
        public int DailyGoal
        {
            get => _dailyGoal;
            set
            {
                if (SetProperty(ref _dailyGoal, value))
                {
                    _settingsService.CurrentSettings.DailyGoal = value;
                    _settingsService.SaveSettings(_settingsService.CurrentSettings);
                }
            }
        }

        private bool _enableSoundEffects;
        public bool EnableSoundEffects
        {
            get => _enableSoundEffects;
            set
            {
                if (SetProperty(ref _enableSoundEffects, value))
                {
                    _settingsService.CurrentSettings.EnableSoundEffects = value;
                    _settingsService.SaveSettings(_settingsService.CurrentSettings);
                }
            }
        }

        private bool _showTranscription;
        public bool ShowTranscription
        {
            get => _showTranscription;
            set
            {
                if (SetProperty(ref _showTranscription, value))
                {
                    _settingsService.CurrentSettings.ShowTranscription = value;
                    _settingsService.SaveSettings(_settingsService.CurrentSettings);
                }
            }
        }

        // --- ПРИВАТНОСТЬ ---
        private bool _keepMeLoggedIn;
        public bool KeepMeLoggedIn
        {
            get => _keepMeLoggedIn;
            set
            {
                if (SetProperty(ref _keepMeLoggedIn, value))
                {
                    _settingsService.CurrentSettings.KeepMeLoggedIn = value;
                    _settingsService.SaveSettings(_settingsService.CurrentSettings);
                }
            }
        }

        private bool _autoSync;
        public bool AutoSync
        {
            get => _autoSync;
            set
            {
                if (SetProperty(ref _autoSync, value))
                {
                    _settingsService.CurrentSettings.AutoSync = value;
                    _settingsService.SaveSettings(_settingsService.CurrentSettings);
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

        private bool _canBecomeTeacher;
        public bool CanBecomeTeacher
        {
            get => _canBecomeTeacher;
            set => SetProperty(ref _canBecomeTeacher, value);
        }

        private bool _isTeacher;
        public bool IsTeacher
        {
            get => _isTeacher;
            set => SetProperty(ref _isTeacher, value);
        }

        // --- СВОЙСТВА АККАУНТА ---
        private string _oldPassword;
        public string OldPassword
        {
            get => _oldPassword;
            set
            {
                if (SetProperty(ref _oldPassword, value))
                {
                    ((RelayCommand)ChangePasswordCommand).RaiseCanExecuteChanged();
                }
            }
        }

        private string _newPassword;
        public string NewPassword
        {
            get => _newPassword;
            set
            {
                if (SetProperty(ref _newPassword, value))
                {
                    ((RelayCommand)ChangePasswordCommand).RaiseCanExecuteChanged();
                }
            }
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

        // --- ЦВЕТА ---
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

        // БУФЕР ОБМЕНА
        private void CopyTeacherCode(object obj)
        {
            if (!string.IsNullOrEmpty(TeacherCode))
            {
                Clipboard.SetText(TeacherCode);
                EventAggregator.Instance.Publish(ShowNotificationMessage.Success(
                    "Скопировано",
                    "Код приглашения скопирован в буфер обмена"));
            }
        }

        // ============================================================
        // КОНСТРУКТОР
        // ============================================================
        public SettingsViewModel(SettingsService settingsService, IDataService dataService, User currentUser)
        {
            SetLocalizedTitle("Loc.Tab.Settings");
            _settingsService = settingsService;
            _dataService = dataService;

            if (currentUser != null)
            {
                TeacherCode = currentUser.InviteCode;
                
                // Определяем роль пользователя
                var roleName = currentUser.Role?.Name ?? "";
                IsTeacher = roleName == "Teacher";
                // User и Admin могут стать учителем, Teacher и Student - нет
                CanBecomeTeacher = roleName == "User" || roleName == "Admin";
            }

            // Инициализация внешнего вида
            _selectedTheme = _settingsService.CurrentSettings.Theme;
            _selectedFontSize = _settingsService.CurrentSettings.BaseFontSize;
            _selectedFontFamily = _settingsService.CurrentSettings.FontFamily;
            _enableAnimations = _settingsService.CurrentSettings.EnableAnimations;

            // Инициализация уведомлений
            _enableNotifications = _settingsService.CurrentSettings.EnableNotifications;
            _notificationDuration = _settingsService.CurrentSettings.NotificationDurationSeconds;

            // Инициализация обучения
            _dailyGoal = _settingsService.CurrentSettings.DailyGoal;
            _enableSoundEffects = _settingsService.CurrentSettings.EnableSoundEffects;
            _showTranscription = _settingsService.CurrentSettings.ShowTranscription;

            // Инициализация приватности
            _keepMeLoggedIn = _settingsService.CurrentSettings.KeepMeLoggedIn;
            _autoSync = _settingsService.CurrentSettings.AutoSync;

            // Инициализация языка
            string currentCode = _settingsService.CurrentSettings.Language;
            _currentLanguage = _languagesMap.FirstOrDefault(x => x.Value == currentCode).Key ?? "English";

            // Команды
            LogoutCommand = new RelayCommand(PerformLogout);
            SwitchSectionCommand = new RelayCommand(sec => CurrentSection = (string)sec);
            UpgradeToTeacherCommand = new RelayCommand(
                async (_) => await PerformUpgradeToTeacher(),
                (_) => CanBecomeTeacher);
            CopyTeacherCodeCommand = new RelayCommand(CopyTeacherCode);

            ChangePasswordCommand = new RelayCommand(
                async (param) => await ChangePasswordAsync(NewPassword),
                (param) => !string.IsNullOrWhiteSpace(OldPassword) && !string.IsNullOrWhiteSpace(NewPassword) 
            );

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
                var result = await _dataService.UpgradeToTeacherAsync();
                
                if (result == null)
                {
                    throw new InvalidOperationException("Не удалось получить ответ от сервера");
                }
                
                TeacherCode = result.InviteCode;

                EventAggregator.Instance.Publish(ShowNotificationMessage.Success(
                    "Статус обновлен",
                    $"Поздравляем! Вы стали учителем. Ваш код: {result.InviteCode}"));

                EventAggregator.Instance.Publish(new RoleChangedMessage { NewToken = result.AccessToken, NewRole = result.UserRole });
            }
            catch (HttpRequestException ex)
            {
                EventAggregator.Instance.Publish(ShowNotificationMessage.Error(
                    "Не удалось обновить статус",
                    $"Ошибка сервера: {ex.Message}"));
            }
            catch (Exception ex)
            {
                EventAggregator.Instance.Publish(ShowNotificationMessage.Error(
                    "Ошибка",
                    ex.Message));
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