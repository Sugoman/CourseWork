using LearningTrainer.Services;
using System.Windows;

namespace LearningTrainer.Core
{
    public abstract class TabViewModelBase : ObservableObject
    {
        private string _title;
        public string Title 
        { 
            get => _title;
            set => SetProperty(ref _title, value);
        }

        /// <summary>
        /// Ключ локализации для названия вкладки (например, "Loc.Tab.Home")
        /// </summary>
        protected string TitleLocalizationKey { get; set; }

        /// <summary>
        /// Суффикс для названия вкладки (например, ": Dictionary Name")
        /// </summary>
        protected string TitleSuffix { get; set; } = "";

        protected TabViewModelBase()
        {
            LanguageService.LanguageChanged += OnLanguageChanged;
        }

        private void OnLanguageChanged(string langCode)
        {
            UpdateLocalizedTitle();
        }

        /// <summary>
        /// Обновляет локализованное название вкладки
        /// </summary>
        protected virtual void UpdateLocalizedTitle()
        {
            if (!string.IsNullOrEmpty(TitleLocalizationKey))
            {
                var localizedTitle = GetLocalized(TitleLocalizationKey);
                Title = string.IsNullOrEmpty(TitleSuffix) 
                    ? localizedTitle 
                    : $"{localizedTitle}{TitleSuffix}";
            }
        }

        /// <summary>
        /// Устанавливает локализованное название вкладки
        /// </summary>
        protected void SetLocalizedTitle(string localizationKey, string suffix = "")
        {
            TitleLocalizationKey = localizationKey;
            TitleSuffix = suffix;
            UpdateLocalizedTitle();
        }

        protected static string GetLocalized(string key)
        {
            try
            {
                return Application.Current.FindResource(key) as string ?? key;
            }
            catch
            {
                return key;
            }
        }
    }
}
