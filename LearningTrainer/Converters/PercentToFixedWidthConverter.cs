using System.Globalization;
using System.Windows.Data;

namespace LearningTrainer.Converters
{
    public class PercentToFixedWidthConverter : IValueConverter
    {
        public double MaxWidth { get; set; } = 400;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double percent)
                return Math.Max(0, Math.Min(MaxWidth, percent / 100.0 * MaxWidth));
            return 0d;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
