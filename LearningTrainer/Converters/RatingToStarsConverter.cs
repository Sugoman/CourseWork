using System;
using System.Globalization;
using System.Windows.Data;

namespace LearningTrainer.Converters
{
    /// <summary>
    /// Конвертирует числовой рейтинг (1-5) в строку звёздочек
    /// Пример: 4 -> "★★★★☆", 3.5 -> "★★★★☆" (округление)
    /// </summary>
    public class RatingToStarsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double rating = 0;
            
            if (value is int intVal)
                rating = intVal;
            else if (value is double doubleVal)
                rating = doubleVal;
            else if (value is float floatVal)
                rating = floatVal;

            int filledStars = (int)Math.Round(rating);
            filledStars = Math.Max(0, Math.Min(5, filledStars));
            
            int emptyStars = 5 - filledStars;
            
            return new string('★', filledStars) + new string('☆', emptyStars);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
