using LearningTrainer.Core;
using LearningTrainerShared.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
            set => SetProperty(ref _words, value);
        }

        public int WordCount => Words?.Count ?? 0;

        public DictionaryViewModel(Dictionary dictionary)
        {
            Model = dictionary;

            Words = new ObservableCollection<Word>(dictionary.Words ?? new List<Word>());

            Words.CollectionChanged += (sender, e) =>
            {
                OnPropertyChanged(nameof(WordCount));
            };
        }
        
    }
}