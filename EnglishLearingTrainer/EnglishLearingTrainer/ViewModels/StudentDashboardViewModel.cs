using EnglishLearingTrainer.Core;
using EnglishLearingTrainer.Models;
using EnglishLearningTrainer.Core;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace EnglishLearningTrainer.ViewModels
{
    public class StudentDashboardViewModel : TabViewModelBase
    {
        public ObservableCollection<Dictionary> Dictionaries { get; set; }

        public ICommand StartLearningCommand { get; }

        public StudentDashboardViewModel()
        {
            // Устанавливаем заголовок для вкладки
            Title = "Главный экран";

            Dictionaries = new ObservableCollection<Dictionary>();
            LoadDictionaries();

            StartLearningCommand = new RelayCommand(StartLearning, CanStartLearning);
        }

        private void LoadDictionaries()
        {
            // В будущем эти данные будут приходить из базы данных
            Dictionaries.Add(new Dictionary
            {
                Id = 1,
                Name = "A1: Beginner English",
                LanguageFrom = "English",
                LanguageTo = "Russian",
                WordCount = 150,
                Description = "Базовые слова для начинающих."
            });
            Dictionaries.Add(new Dictionary
            {
                Id = 2,
                Name = "B2: Upper-Intermediate",
                LanguageFrom = "English",
                LanguageTo = "Russian",
                WordCount = 500,
                Description = "Лексика для свободного общения."
            });
            Dictionaries.Add(new Dictionary
            {
                Id = 3,
                Name = "IT Terminology",
                LanguageFrom = "English",
                LanguageTo = "Russian",
                WordCount = 250,
                Description = "Специализированные термины для айтишников."
            });
        }

        private bool CanStartLearning(object parameter)
        {
            var canExecute = parameter is Dictionary;
            System.Diagnostics.Debug.WriteLine($"CanStartLearning: {canExecute}, Parameter: {parameter?.GetType().Name}");
            return canExecute;
        }

        public void StartLearning(object parameter)
        {
            System.Diagnostics.Debug.WriteLine("StartLearning command executed!");

            if (parameter is Dictionary dictionary)
            {
                System.Diagnostics.Debug.WriteLine($"Dictionary: {dictionary.Name}");
                var learningVm = new LearningViewModel(dictionary);

                // Проверяем EventAggregator
                if (EventAggregator.Instance != null)
                {
                    System.Diagnostics.Debug.WriteLine("Publishing LearningViewModel...");
                    EventAggregator.Instance.Publish(learningVm);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("EventAggregator is NULL!");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Parameter is not Dictionary!");
            }
        }
    }
}
