using LearningTrainer.Services;
using LearningTrainer.ViewModels;
using System.Configuration;
using System.Data;
using System.Globalization;
using System.Net.Http;
using System.Windows;

namespace LearningTrainer
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            var settingsService = new SettingsService();

            SessionService sessionService = new SessionService();
            var savedSession = sessionService.LoadSession();

            MainViewModel mainVM = savedSession != null
                ? new MainViewModel(savedSession)
                : new MainViewModel();

            var mainWindow = new MainWindow();
            mainWindow.DataContext = mainVM;
            mainWindow.Show();

            base.OnStartup(e);
        }
    }

}
