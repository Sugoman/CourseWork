using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace LearningTrainer.Behaviors
{
    public static class PasswordBoxBehavior
    {
        public static readonly DependencyProperty SubmitCommandProperty =
            DependencyProperty.RegisterAttached("SubmitCommand", typeof(ICommand), typeof(PasswordBoxBehavior), new PropertyMetadata(null));


        public static ICommand GetSubmitCommand(DependencyObject obj) => (ICommand)obj.GetValue(SubmitCommandProperty);
        public static void SetSubmitCommand(DependencyObject obj, ICommand value) => obj.SetValue(SubmitCommandProperty, value);

        public static readonly DependencyProperty PasswordProperty =
            DependencyProperty.RegisterAttached("Password", typeof(string), typeof(PasswordBoxBehavior), new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnPasswordPropertyChanged));

        public static string GetPassword(DependencyObject obj) => (string)obj.GetValue(PasswordProperty);
        public static void SetPassword(DependencyObject obj, string value) => obj.SetValue(PasswordProperty, value);


        public static readonly DependencyProperty AttachProperty =
            DependencyProperty.RegisterAttached("Attach", typeof(bool), typeof(PasswordBoxBehavior), new PropertyMetadata(false, OnAttachChanged));

        public static bool GetAttach(DependencyObject dp) => (bool)dp.GetValue(AttachProperty);
        public static void SetAttach(DependencyObject dp, bool value) => dp.SetValue(AttachProperty, value);


        // --- ОБРАБОТЧИКИ ---

        private static void OnAttachChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is PasswordBox passwordBox)
            {
                if ((bool)e.NewValue)
                {
                    passwordBox.PasswordChanged += PasswordBox_PasswordChanged;
                    passwordBox.KeyDown += PasswordBox_KeyDown;
                }
                else
                {
                    passwordBox.PasswordChanged -= PasswordBox_PasswordChanged;
                    passwordBox.KeyDown -= PasswordBox_KeyDown;
                }
            }
        }

        private static void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (sender is PasswordBox passwordBox)
            {
                SetPassword(passwordBox, passwordBox.Password);
            }
        }

        private static void OnPasswordPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
        }

        private static void PasswordBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var passwordBox = sender as PasswordBox;
                var command = GetSubmitCommand(passwordBox);

                if (command != null && command.CanExecute(passwordBox.Password))
                {
                    command.Execute(passwordBox.Password);
                    e.Handled = true;
                }
            }
        }
    }
}
