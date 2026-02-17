using LearningTrainer.Core;
using LearningTrainerShared.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using static LearningTrainer.Core.EventAggregator;

namespace LearningTrainer.ViewModels
{
    public class DictionaryViewModel : ObservableObject
    {
        public Dictionary Model { get; }

        public int Id => Model.Id;
        public string Name
        {
            get => Model.Name;
            set
            {
                if (Model.Name == value) return;
                Model.Name = value;
                OnPropertyChanged();
            }
        }
        public string Description
        {
            get => Model.Description;
            set
            {
                if (Model.Description == value) return;
                Model.Description = value;
                OnPropertyChanged();
            }
        }

        private bool _isFeatured;
        public bool IsFeatured
        {
            get => _isFeatured;
            set => SetProperty(ref _isFeatured, value);
        }

        public string LanguageFrom
        {
            get => Model.LanguageFrom;
            set
            {
                if (Model.LanguageFrom == value) return;
                Model.LanguageFrom = value;
                OnPropertyChanged();
            }
        }

        public string LanguageTo
        {
            get => Model.LanguageTo;
            set
            {
                if (Model.LanguageTo == value) return;
                Model.LanguageTo = value;
                OnPropertyChanged();
            }
        }

        private ObservableCollection<Word> _words;
        public ObservableCollection<Word> Words
        {
            get => _words;
            set
            {
                if (SetProperty(ref _words, value))
                {
                    OnPropertyChanged(nameof(WordCount));
                }
            }
        }

        private int _cachedWordCount;
        public int WordCount => Words?.Count > 0 && Words.Any(w => !string.IsNullOrEmpty(w.OriginalWord))
            ? Words.Count
            : _cachedWordCount;

        public DictionaryViewModel(Dictionary dictionary)
        {
            Model = dictionary;
            _cachedWordCount = dictionary.WordCount;

            // Only populate Words if they contain real data (not dummy placeholders)
            var realWords = dictionary.Words?.Where(w => !string.IsNullOrEmpty(w.OriginalWord)).ToList();
            Words = new ObservableCollection<Word>(realWords ?? new List<Word>());

            Words.CollectionChanged += (sender, e) =>
            {
                OnPropertyChanged(nameof(WordCount));
            };
        }
        
    }
}
