using EnglishLearningTrainer.Models;
using EnglishLearningTrainer.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace EnglishLearningTrainer.Views
{
    /// <summary>
    /// Логика взаимодействия для DashboardView.xaml
    /// </summary>
    public partial class DashboardView : UserControl
    {

        public DashboardView()
        {
            InitializeComponent();
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
                var viewModel = this.DataContext as DashboardViewModel;
                viewModel?.StartLearning(dictionary);
            }
        }


    }
}
