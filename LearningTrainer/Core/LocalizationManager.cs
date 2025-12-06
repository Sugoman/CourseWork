using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace LearningTrainer.Core
{
    public static class LocalizationManager
    {
        public static void SetLanguage(string cultureCode)
        {
            var dictionaryUri = new Uri($"Resources/Languages/Lang.{cultureCode}.xaml", UriKind.Relative);
            var newDict = new ResourceDictionary { Source = dictionaryUri };

            var oldDict = Application.Current.Resources.MergedDictionaries
                                     .FirstOrDefault(d => d.Source != null && d.Source.OriginalString.Contains("Lang."));

            if (oldDict != null)
            {
                Application.Current.Resources.MergedDictionaries.Remove(oldDict);
            }

            Application.Current.Resources.MergedDictionaries.Add(newDict);
        }
    }
}
