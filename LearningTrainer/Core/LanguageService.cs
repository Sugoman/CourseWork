using System;
using System.Linq;
using System.Windows;

namespace LearningTrainer.Services
{
    public static class LanguageService
    {
        /// <summary>
        /// Событие, вызываемое при смене языка
        /// </summary>
        public static event Action<string> LanguageChanged;

        public static void SetLanguage(string langName) 
        {
            string uriStr = $"/Resources/Languages/Lang.{langName}.xaml";
            var uri = new Uri(uriStr, UriKind.RelativeOrAbsolute);

            var newDict = new ResourceDictionary { Source = uri };

            var appDictionaries = Application.Current.Resources.MergedDictionaries;
            // Try to find already loaded language dictionary. Previously code searched for "Languages." which
            // doesn't match typical resource URIs (they contain "/Resources/Languages/..."). Use a more
            // robust check to find any merged dictionary from the Languages folder.
            var oldDict = appDictionaries.FirstOrDefault(d => d.Source != null && d.Source.OriginalString.Contains("Resources/Languages"));

            if (oldDict != null)
            {
                appDictionaries.Remove(oldDict);
                appDictionaries.Add(newDict);
            }
            else
            {
                appDictionaries.Add(newDict);
            }

            // Уведомляем подписчиков о смене языка
            LanguageChanged?.Invoke(langName);
        }
    }
}
