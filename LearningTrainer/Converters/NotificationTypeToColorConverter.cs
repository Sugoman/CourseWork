using LearningTrainer.Services;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace LearningTrainer.Converters
{
    /// <summary>
    /// Конвертер для преобразования типа уведомления в цвет
    /// </summary>
    public class NotificationTypeToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is NotificationType type)
            {
                var color = type switch
                {
                    NotificationType.AccessDenied => "#F87171", // Red
                    NotificationType.Info => "#3B82F6", // Blue
                    NotificationType.Success => "#30A966", // Green
                    NotificationType.Error => "#EF4444", // Dark Red
                    NotificationType.RoleInfo => "#8B5CF6", // Purple
                    NotificationType.Warning => "#FBBF24", // Amber
                    _ => "#6B7280" // Gray
                };

                return new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
            }

            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Конвертер для преобразования типа уведомления в иконку
    /// </summary>
    public class NotificationTypeToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is NotificationType type)
            {
                return type switch
                {
                    NotificationType.AccessDenied => "?",
                    NotificationType.Info => "??",
                    NotificationType.Success => "?",
                    NotificationType.Error => "??",
                    NotificationType.RoleInfo => "??",
                    NotificationType.Warning => "?",
                    _ => "•"
                };
            }

            return "•";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Конвертер для преобразования типа уведомления в шрифт
    /// </summary>
    public class NotificationTypeToFontWeightConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is NotificationType type)
            {
                return type switch
                {
                    NotificationType.Error or NotificationType.AccessDenied => 
                        System.Windows.FontWeights.Bold,
                    _ => System.Windows.FontWeights.Normal
                };
            }

            return System.Windows.FontWeights.Normal;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
