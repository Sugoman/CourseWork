using LearningTrainer.Core;
using LearningTrainer.Services;
using LearningTrainerShared.Models;
using System.Net.Http;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

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

        private bool _isDarkMode;
        public bool IsDarkMode
        {
            get => _isDarkMode;
            set
            {
                if (SetProperty(ref _isDarkMode, value))
                {
                    SaveSettings();
                    _settingsService.ApplyCustomColors(reloadBaseTheme: true);
                    UpdateColorsFromResources();
                }
            }
        }

        // ШРИФТ
        private double _selectedFontSize;
        public double SelectedFontSize
        {
            get => _selectedFontSize;
            set
            {
                if (SetProperty(ref _selectedFontSize, value))
                {
                    SaveSettings();
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

        private string _appBackgroundColor;
        public string AppBackgroundColor
        {
            get => _appBackgroundColor;
            set
            {
                if (SetProperty(ref _appBackgroundColor, value))
                {
                    SaveSettings();
                    _settingsService.ApplyCustomColors();
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
                    SaveSettings();
                    _settingsService.ApplyCustomColors();
                }
            }
        }

        private string _selectedTheme;
        public string SelectedTheme
        {
            get => _selectedTheme;
            set
            {
                if (SetProperty(ref _selectedTheme, value))
                {
                    _settingsService.ApplyTheme(value);
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
                    SaveSettings();
                    _settingsService.ApplyCustomColors();
                }
            }
        }

        // --- СВОЙСТВА ЛОКАЛИЗАЦИИ ---
        public List<string> AvailableLanguages { get; } = new List<string> { "English", "Русский", "Español", "Deutsch" };
        public List<string> AvailableThemes { get; } = new List<string> { "Light", "Dark", "Dracula", "Forest" };

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

            _isDarkMode = _settingsService.CurrentSettings.Theme == "Dark";

            LogoutCommand = new RelayCommand(PerformLogout);
            SwitchSectionCommand = new RelayCommand(sec => CurrentSection = (string)sec);

            SelectedTheme = _settingsService.CurrentSettings.Theme;
            UpgradeToTeacherCommand = new RelayCommand(async (_) => await PerformUpgradeToTeacher());

            ChangePasswordCommand = new RelayCommand(
                async (param) => await ChangePasswordAsync((string)param),
                (param) => !string.IsNullOrWhiteSpace(OldPassword) && !string.IsNullOrWhiteSpace((string)param)
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
            // Хелпер для вытаскивания цвета в HEX
            string GetHex(string key)
            {
                if (Application.Current.Resources[key] is SolidColorBrush brush)
                {
                    return brush.Color.ToString(); // Вернет типа #FF121212
                }
                return "#000000"; // Фолбэк на всякий случай
            }

            // ВАЖНО: Обновляем свойства через поле и RaisePropertyChanged, 
            // чтобы НЕ триггерить лишнее сохранение настроек (если у тебя в сеттере стоит Save logic)

            _appBackgroundColor = GetHex("MainBackgroundBrush");
            OnPropertyChanged(nameof(AppBackgroundColor));

            _appTextColor = GetHex("PrimaryTextBrush");
            OnPropertyChanged(nameof(AppTextColor));

            _appAccentColor = GetHex("PrimaryAccentBrush");
            OnPropertyChanged(nameof(AppAccentColor));

            // Добавь сюда остальные свои цвета (Border, Input и т.д.)
        }
        private void SaveSettings()
        {
            var newSettings = new SettingsModel
            {
                Theme = IsDarkMode ? "Dark" : "Light",
                BaseFontSize = SelectedFontSize
            };
            _settingsService.SaveSettings(newSettings);
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