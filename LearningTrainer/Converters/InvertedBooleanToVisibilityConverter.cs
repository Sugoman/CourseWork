using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace LearningTrainer.Converters
{
    /// <summary>
    /// Конвертирует булево значение в Visibility инвертированно. (true -> Collapsed, false -> Visible)
    /// </summary>
    public class InvertedBooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool boolValue = (value is bool b) && b;
            return !boolValue ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
