using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace LearningTrainer.Converters
{
    /// <summary>
    /// Конвертер процентного значения в ширину относительно родительского элемента
    /// </summary>
    public class PercentToWidthConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 2) return 0d;

            if (values[0] is double percent && values[1] is double containerWidth)
            {
                return Math.Max(0, Math.Min(containerWidth, containerWidth * percent / 100));
            }

            return 0d;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Конвертер количества в прозрачность (для тепловых карт)
    /// </summary>
    public class CountToOpacityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int count)
            {
                // Нормализуем значение: 0 слов = 0.2, 50+ слов = 1.0
                return Math.Min(1.0, 0.2 + count / 60.0);
            }
            return 0.2;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Конвертер bool в прозрачность (для заблокированных достижений)
    /// </summary>
    public class BoolToOpacityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isUnlocked)
            {
                return isUnlocked ? 1.0 : 0.4;
            }
            return 1.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Конвертер активности в цвет для тепловой карты (GitHub-стиль)
    /// </summary>
    public class ActivityToHeatmapColorConverter : IValueConverter
    {
        private static readonly string[] HeatmapColors = 
        {
            "#EBEDF0", // 0 - пусто
            "#9BE9A8", // 1-10
            "#40C463", // 11-25
            "#30A14E", // 26-50
            "#216E39"  // 50+
        };

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int count)
            {
                int index = count switch
                {
                    0 => 0,
                    <= 10 => 1,
                    <= 25 => 2,
                    <= 50 => 3,
                    _ => 4
                };
                return HeatmapColors[index];
            }
            return HeatmapColors[0];
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Конвертер прогресса достижения в ширину прогресс-бара
    /// </summary>
    public class ProgressToWidthConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 2 && values[0] is double progress && values[1] is double containerWidth)
            {
                return Math.Max(0, Math.Min(containerWidth, containerWidth * progress / 100.0));
            }
            return 0d;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Фильтр для отображения только активных достижений (in-progress или недавно разблокированных)
    /// </summary>
    public class AchievementVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is LearningTrainerShared.Models.Statistics.Achievement achievement)
            {
                // Показываем если: разблокировано недавно (7 дней) или есть прогресс > 0%
                if (achievement.IsUnlocked && achievement.UnlockedAt.HasValue)
                {
                    return (DateTime.UtcNow - achievement.UnlockedAt.Value).TotalDays <= 7 
                        ? Visibility.Visible 
                        : Visibility.Collapsed;
                }
                return achievement.Progress > 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
