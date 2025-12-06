using LearningTrainer.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

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
