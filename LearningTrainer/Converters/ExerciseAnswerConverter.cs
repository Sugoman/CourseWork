using System;
using System.Globalization;
using System.Windows.Data;

namespace LearningTrainer.Converters
{
    /// <summary>
    /// MultiValueConverter для передачи ExerciseViewModel и индекса ответа в команду.
    /// Возвращает object[] { exerciseVM, answerIndex }.
    /// </summary>
    public class ExerciseAnswerConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length == 2 && values[0] != null)
            {
                int answerIndex = 0;
                if (values[1] is int i)
                    answerIndex = i;
                else if (values[1] is string s && int.TryParse(s, out var parsed))
                    answerIndex = parsed;

                return new object[] { values[0], answerIndex };
            }
            return values;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
