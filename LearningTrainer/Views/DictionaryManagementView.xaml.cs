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
    /// Логика взаимодействия для DictionaryManagementView.xaml
    /// </summary>
    public partial class DictionaryManagementView : UserControl
    {
        public DictionaryManagementView()
        {
            InitializeComponent();

            // 🔥 ПОСТАВЬ ТОЧКУ ОСТАНОВА ЗДЕСЬ
            this.Loaded += (s, e) =>
            {
                if (this.DataContext == null)
                {
                    // Если ты здесь, значит, в ShellView неверно привязан DataContext.
                }
                else
                {
                    // Должно быть: DictionaryManagementViewModel
                }
            };
        }
    }
}
