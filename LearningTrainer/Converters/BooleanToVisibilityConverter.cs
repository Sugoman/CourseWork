using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace LearningTrainer.Converters
{

    /// <summary>
    /// Конвертирует булево значение в Visibility. (true -> Visible, false -> Collapsed)
    /// Если параметр "invert", то поведение обратное.
    /// </summary>
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool boolValue = (value is bool b) && b;

            if (parameter as string == "invert")
            {
                boolValue = !boolValue;
            }

            return boolValue ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
