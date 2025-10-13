using EnglishLearingTrainer.Core;
using EnglishLearingTrainer.Models;
using EnglishLearningTrainer.Core;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using static EnglishLearningTrainer.Core.EventAggregator;

namespace EnglishLearningTrainer.ViewModels
{
    public class LearningViewModel : TabViewModelBase
    {
        // --- ФИКС №2: Меняем хрупкий List на надежную Очередь (Queue) ---
        private readonly Queue<Word> _wordQueue;

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
            set => SetProperty(ref _isFlipped, value);
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

            var words = new List<Word>
            {
                new Word { Id = 1, OriginalWord = "Apple", Translation = "Яблоко" },
                new Word { Id = 2, OriginalWord = "Book", Translation = "Книга" },
                new Word { Id = 3, OriginalWord = "Car", Translation = "Машина" },
                new Word { Id = 4, OriginalWord = "House", Translation = "Дом" },
                new Word { Id = 5, OriginalWord = "Tree", Translation = "Дерево" }
            };
            // Создаем очередь из нашего списка слов
            _wordQueue = new Queue<Word>(words);

            // Показываем первое слово, не вынимая его из очереди
            CurrentWord = _wordQueue.Any() ? _wordQueue.Peek() : null;

            AnswerCommand = new RelayCommand(async (param) => await HandleAnswerAsync((bool)param), (param) => !_isFlipped);
            CloseTabCommand = new RelayCommand(CloseTab);
        }

        private async Task HandleAnswerAsync(bool knowsTheWord)
        {
            IsFlipped = true;
            await Task.Delay(1500);

            // --- НОВАЯ ЛОГИКА НА ОЧЕРЕДИ ---
            // Вынимаем текущее слово из начала очереди
            var processedWord = _wordQueue.Dequeue();

            if (!knowsTheWord)
            {
                // Если не знает, кладем слово в конец очереди
                _wordQueue.Enqueue(processedWord);
            }
            // -----------------------------

            if (!_wordQueue.Any())
            {
                IsSessionComplete = true;
                return;
            }

            IsFlipped = false;
            await Task.Delay(300);

            // Показываем новое первое слово в очереди
            CurrentWord = _wordQueue.Peek();
        }

        private void CloseTab(object parameter)
        {
            EventAggregator.Instance.Publish(new CloseTabMessage { TabToClose = this });
        }
    }
}