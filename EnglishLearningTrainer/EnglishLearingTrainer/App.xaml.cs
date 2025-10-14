using EnglishLearningTrainer.Services;
using System.Configuration;
using System.Data;
using System.Windows;

namespace EnglishLearningTrainer
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            System.Diagnostics.Debug.WriteLine("=== APPLICATION STARTING ===");

            try
            {
                using (var dataService = new DataService())
                {
                    System.Diagnostics.Debug.WriteLine("Calling InitializeTestDataAsync...");
                    await dataService.InitializeTestDataAsync();
                    System.Diagnostics.Debug.WriteLine("InitializeTestDataAsync completed");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CRITICAL ERROR: {ex}");
                MessageBox.Show($"Database error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

}
