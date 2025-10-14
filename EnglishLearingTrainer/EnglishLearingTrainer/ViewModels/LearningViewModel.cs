using EnglishLearningTrainer.Core;
using EnglishLearningTrainer.Models;
using System.Windows;
using System.Windows.Input;
using static EnglishLearningTrainer.Core.EventAggregator;

namespace EnglishLearningTrainer.ViewModels
{
    public class LearningViewModel : TabViewModelBase
    {
        private Queue<Word> _wordsQueue;

        private Word _currentWord;
        public Word CurrentWord
        {
            get => _currentWord;
            set => SetProperty(ref _currentWord, value);
        }


        private bool _isFlipped;
        public bool IsFlipped
        {
            get => _isFlipped;
            set
            {
                if (SetProperty(ref _isFlipped, value))
                {
                    (AnswerCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        private bool _isSessionComplete;
        public bool IsSessionComplete
        {
            get => _isSessionComplete;
            set => SetProperty(ref _isSessionComplete, value);
        }

        public ICommand AnswerCommand { get; }
        public ICommand CloseTabCommand { get; }

        public LearningViewModel(Dictionary dictionary)
        {
            Title = $"Изучение: {dictionary.Name}";

            _wordsQueue = new Queue<Word>(dictionary.Words);
            if (_wordsQueue.Count == 0)
            {
                MessageBox.Show("Словарь пуст");
                return;
            }
            else
            {
                CurrentWord = _wordsQueue.FirstOrDefault();

                AnswerCommand = new RelayCommand(async (param) => await HandleAnswerAsync((bool)param), (param) => !_isFlipped);
                CloseTabCommand = new RelayCommand(CloseTab);
            }
                
        }

        private async Task HandleAnswerAsync(bool knowsTheWord)
        {
            IsFlipped = true;
            await Task.Delay(1500);

            var word = _wordsQueue.Dequeue();

            if (!knowsTheWord)
            {
                _wordsQueue.Enqueue(word);
            }

            if (!_wordsQueue.Any())
            {
                IsSessionComplete = true;
                return;
            }

            IsFlipped = false;
            await Task.Delay(300);
            CurrentWord = _wordsQueue.Peek();
        }

        private void CloseTab(object parameter)
        {
            EventAggregator.Instance.Publish(new CloseTabMessage(this));
        }
    }
}