using LearningTrainer.ViewModels;
using LearningTrainerShared.Models;
using System.Windows;
using System.Windows.Controls;

namespace LearningTrainer.Views
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

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.ContextMenu != null)
            {
                button.ContextMenu.PlacementTarget = button;
                button.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                button.ContextMenu.IsOpen = true;
            }
        }
    }
}
