using LearningTrainer.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace LearningTrainer.Views
{
    /// <summary>
    /// Логика взаимодействия для LoginView.xaml
    /// </summary>
    public partial class LoginView : UserControl
    {
        public LoginView()
        {
            InitializeComponent();
        }

        private async void SubmitButton_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as LoginViewModel;
            if (vm == null) return;

            string pass = MyPasswordBox.Password;
            string confirmPass = ConfirmPasswordBox.Password;

            await vm.PerformSubmit(pass, confirmPass);
        }
    }
}
