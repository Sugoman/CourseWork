using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace LearningTrainer.Converters
{
    /// <summary>
    /// Конвертирует строку в Visibility.
    /// Если строка null или пустая, возвращает Visible.
    /// Если в строке есть текст, возвращает Collapsed.
    /// Используется для плейсхолдеров.
    /// Если параметр "invert", поведение обратное.
    /// </summary>
    public class StringNullOrEmptyToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isNullOrEmpty = string.IsNullOrEmpty(value as string);

            if (parameter as string == "invert")
            {
                isNullOrEmpty = !isNullOrEmpty;
            }

            return isNullOrEmpty ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}