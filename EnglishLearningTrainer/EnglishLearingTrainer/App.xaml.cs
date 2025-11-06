using EnglishLearningTrainer.Services;
using EnglishLearningTrainer.ViewModels;
using System.Configuration;
using System.Data;
using System.Net.Http;
using System.Windows;

namespace EnglishLearningTrainer
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            SessionService sessionService = new SessionService();
            var savedSession = sessionService.LoadSession();

            MainViewModel mainVM;

            if (savedSession != null)
            {
                mainVM = new MainViewModel(savedSession);
            }
            else
            {
                mainVM = new MainViewModel();
            }

            var mainWindow = new MainWindow();
            mainWindow.DataContext = mainVM;
            mainWindow.Show();

            base.OnStartup(e);
        }
    }

}
