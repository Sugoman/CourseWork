using EnglishLearingTrainer.Models;
using EnglishLearningTrainer.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace EnglishLearningTrainer.Views
{
    /// <summary>
    /// Логика взаимодействия для StudentDashboardView.xaml
    /// </summary>
    public partial class StudentDashboardView : UserControl
    {
        public StudentDashboardView()
        {
            InitializeComponent();
            // Для отладки проверьте DataContext
            this.Loaded += (s, e) =>
            {
                if (this.DataContext == null)
                {
                    System.Diagnostics.Debug.WriteLine("DataContext is null!");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"DataContext type: {this.DataContext.GetType().Name}");
                }
            };
        }
        private void DebugButtonClick(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var dictionary = button?.DataContext as Dictionary;

            System.Diagnostics.Debug.WriteLine($"Button clicked! Dictionary: {dictionary?.Name}");

            if (dictionary != null)
            {
                // Прямой вызов без команд
                var viewModel = this.DataContext as StudentDashboardViewModel;
                viewModel?.StartLearning(dictionary);
            }
        }


    }
}
