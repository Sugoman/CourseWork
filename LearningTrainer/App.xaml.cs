using LearningTrainer.Services;
using LearningTrainer.ViewModels;
using System.Configuration;
using System.Data;
using System.Globalization;
using System.Net.Http;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.IO;

namespace LearningTrainer
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private IConfiguration _configuration;
        private IServiceProvider _serviceProvider;

        protected override void OnStartup(StartupEventArgs e)
        {
            // Загрузить конфигурацию
            var configBuilder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

            _configuration = configBuilder.Build();

            var settingsService = new SettingsService();

            SessionService sessionService = new SessionService();
            var savedSession = sessionService.LoadSession();

            MainViewModel mainVM = savedSession != null
                ? new MainViewModel(savedSession, _configuration)
                : new MainViewModel(_configuration);

            var mainWindow = new MainWindow();
            mainWindow.DataContext = mainVM;
            mainWindow.Show();

            base.OnStartup(e);
        }
    }

}
