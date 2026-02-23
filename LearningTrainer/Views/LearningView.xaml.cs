using LearningTrainer.ViewModels;
using LearningTrainerShared.Models;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace LearningTrainer.Views
{
    /// <summary>
    /// Логика взаимодействия для LearningView.xaml
    /// </summary>
    public partial class LearningView : UserControl
    {
        public LearningView()
        {
            InitializeComponent();
            Loaded += (s, e) => Focus();
        }

        private void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (DataContext is not LearningViewModel vm) return;
            if (!vm.IsExerciseTypeChosen || vm.IsSessionComplete) return;

            if (e.Key == Key.Escape)
            {
                if (vm.SkipWordCommand.CanExecute(null))
                    vm.SkipWordCommand.Execute(null);
                e.Handled = true;
                return;
            }

            if (vm.IsFlashcardMode)
                HandleFlashcardKey(vm, e);
            else if (vm.IsMcqMode)
                HandleMcqKey(vm, e);
            else if (vm.IsTypingMode)
                HandleTypingKey(vm, e);
            else if (vm.IsListeningMode)
                HandleListeningKey(vm, e);
        }

        private static void HandleFlashcardKey(LearningViewModel vm, KeyEventArgs e)
        {
            if (!vm.IsFlipped)
            {
                if (e.Key == Key.Space)
                {
                    if (vm.FlipCardCommand.CanExecute(null))
                        vm.FlipCardCommand.Execute(null);
                    e.Handled = true;
                }
            }
            else
            {
                ResponseQuality? quality = e.Key switch
                {
                    Key.D1 or Key.NumPad1 => ResponseQuality.Again,
                    Key.D2 or Key.NumPad2 => ResponseQuality.Hard,
                    Key.D3 or Key.NumPad3 => ResponseQuality.Good,
                    Key.D4 or Key.NumPad4 => ResponseQuality.Easy,
                    _ => null
                };

                if (quality.HasValue && vm.AnswerCommand.CanExecute(quality.Value))
                {
                    vm.AnswerCommand.Execute(quality.Value);
                    e.Handled = true;
                }
            }
        }

        private static void HandleMcqKey(LearningViewModel vm, KeyEventArgs e)
        {
            if (!vm.McqAnswered)
            {
                int index = e.Key switch
                {
                    Key.D1 or Key.NumPad1 => 0,
                    Key.D2 or Key.NumPad2 => 1,
                    Key.D3 or Key.NumPad3 => 2,
                    Key.D4 or Key.NumPad4 => 3,
                    _ => -1
                };

                if (index >= 0 && index < vm.McqOptions.Count)
                {
                    var optionItem = vm.McqOptions[index];
                    if (vm.SelectMcqOptionCommand.CanExecute(optionItem))
                        vm.SelectMcqOptionCommand.Execute(optionItem);
                    e.Handled = true;
                }
            }
            else if (e.Key is Key.Enter or Key.Space)
            {
                if (vm.NextMcqWordCommand.CanExecute(null))
                    vm.NextMcqWordCommand.Execute(null);
                e.Handled = true;
            }
        }

        private static void HandleTypingKey(LearningViewModel vm, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;

            if (!vm.TypingAnswered)
            {
                if (vm.CheckTypingCommand.CanExecute(null))
                    vm.CheckTypingCommand.Execute(null);
            }
            else
            {
                if (vm.NextTypingWordCommand.CanExecute(null))
                    vm.NextTypingWordCommand.Execute(null);
            }
            e.Handled = true;
        }

        private static void HandleListeningKey(LearningViewModel vm, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (!vm.ListeningAnswered)
                {
                    if (vm.CheckListeningCommand.CanExecute(null))
                        vm.CheckListeningCommand.Execute(null);
                }
                else
                {
                    if (vm.NextListeningWordCommand.CanExecute(null))
                        vm.NextListeningWordCommand.Execute(null);
                }
                e.Handled = true;
            }
            else if (e.Key == Key.R && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
            {
                if (vm.ReplayListeningCommand.CanExecute(null))
                    vm.ReplayListeningCommand.Execute(null);
                e.Handled = true;
            }
        }
    }
}
