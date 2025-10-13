using EnglishLearingTrainer.Core;

namespace EnglishLearningTrainer.ViewModels
{
    // Главная ViewModel, которая управляет навигацией
    public class MainViewModel : ObservableObject
    {
        private object _currentView;
        public object CurrentView
        {
            get { return _currentView; }
            set
            {
                _currentView = value;
                OnPropertyChanged();
            }
        }

        public MainViewModel()
        {
            var loginVM = new LoginViewModel();

            loginVM.LoginSuccessful += OnLoginSuccessful;

            CurrentView = loginVM;
        }

        private void OnLoginSuccessful()
        {
            CurrentView = new ShellViewModel();
        }
    }

}
