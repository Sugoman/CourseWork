using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace LearningTrainer.Services
{
    public static class ThemeService
    {
        // 1. Смена словаря (Light <-> Dark)
        public static void SetTheme(string themeName)
        {
            var appResources = Application.Current.Resources.MergedDictionaries;

            // Удаляем старый словарь темы (ищем по названию файла)
            var existingTheme = appResources.FirstOrDefault(d =>
                d.Source != null && d.Source.OriginalString.Contains("Theme."));

            if (existingTheme != null) appResources.Remove(existingTheme);

            // Добавляем новый (Pack URI - железобетонный путь)
            string uri = $"pack://application:,,,/Resources/Themes/Theme.{themeName}.xaml";

            try
            {
                appResources.Add(new ResourceDictionary { Source = new Uri(uri) });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Ошибка загрузки темы: {ex.Message}");
            }
        }

        // 2. Точечная покраска (для кастомных цветов)
        public static void ApplyColor(string resourceKey, string hexColor)
        {
            if (string.IsNullOrEmpty(hexColor)) return;

            try
            {
                var color = (Color)ColorConverter.ConvertFromString(hexColor);
                var brush = new SolidColorBrush(color);

                // Перезаписываем ресурс глобально
                Application.Current.Resources[resourceKey] = brush;
            }
            catch
            {
                System.Diagnostics.Debug.WriteLine($"Invalid color: {hexColor}");
            }
        }
    }
}