using System;
using System.Linq;
using System.Windows;

namespace LearningTrainer.Services
{
    public static class ThemeService
    {
        public static void SetTheme(string themeName) 
        {
            string uriStr = $"/Resources/Theme/Theme.{themeName}.xaml";
            var uri = new Uri(uriStr, UriKind.RelativeOrAbsolute);

            var newDict = new ResourceDictionary { Source = uri };

            var appDictionaries = Application.Current.Resources.MergedDictionaries;
            var oldDict = appDictionaries.FirstOrDefault(d => d.Source != null && d.Source.OriginalString.Contains("Theme."));

            if (oldDict != null)
            {
                appDictionaries.Remove(oldDict);
                appDictionaries.Add(newDict);
            }
            else
            {
                appDictionaries.Add(newDict);
            }
        }
    }
}