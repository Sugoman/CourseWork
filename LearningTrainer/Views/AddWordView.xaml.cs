using LearningTrainer.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace LearningTrainer.Views
{
    /// <summary>
    /// Логика взаимодействия для AddWordView.xaml
    /// </summary>
    public partial class AddWordView : UserControl
    {
        public AddWordView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
            PreviewKeyDown += OnPreviewKeyDown;
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            var vm = DataContext as AddWordViewModel;
            if (vm == null) return;

            if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                if (vm.SaveCommand.CanExecute(null))
                    vm.SaveCommand.Execute(null);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                if (vm.DoneCommand.CanExecute(null))
                    vm.DoneCommand.Execute(null);
                e.Handled = true;
            }
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is AddWordViewModel oldVm)
                oldVm.WordSaved -= OnWordSaved;

            if (e.NewValue is AddWordViewModel newVm)
                newVm.WordSaved += OnWordSaved;
        }

        private void OnWordSaved()
        {
            Dispatcher.BeginInvoke(() =>
            {
                OriginalWordTextBox.Focus();
                OriginalWordTextBox.CaretIndex = 0;
            }, System.Windows.Threading.DispatcherPriority.Input);
        }

        private void OnOriginalWordTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Tab)
            {
                var vm = DataContext as AddWordViewModel;
                if (vm != null)
                {
                    bool suggestionAccepted = vm.AcceptSuggestion();

                    if (suggestionAccepted)
                    {
                        e.Handled = true;

                        OriginalWordTextBox.Focus();
                        OriginalWordTextBox.CaretIndex = OriginalWordTextBox.Text.Length;
                    }
                }
            }
        }
    }
}
