using LearningTrainer.Core;
using LearningTrainer.Services;
using LearningTrainerShared.Models;
using System.Windows;
using System.Windows.Input;

namespace LearningTrainer.ViewModels
{
    public class AddWordViewModel : TabViewModelBase
    {
        private readonly IDataService _dataService;
        private readonly Dictionary _selectedDictionary;
        private readonly SpellCheckService _spellCheckService;

        public string Translation { get; set; }
        public string Example { get; set; }
        private string _suggestion;
        public string Suggestion
        {
            get => _suggestion;
            set => SetProperty(ref _suggestion, value);
        }

        private string _originalWord;
        public string OriginalWord
        {
            get => _originalWord;
            set
            {
                SetProperty(ref _originalWord, value);
                UpdateSuggestion();
            }
        }

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }

        public AddWordViewModel(IDataService dataService, Dictionary dictionary)
        {
            _dataService = dataService;
            _selectedDictionary = dictionary;
            _spellCheckService = new SpellCheckService();
            
            SetLocalizedTitle("Loc.Tab.AddWord", $": {dictionary.Name}");

            SaveCommand = new RelayCommand(async (param) => await SaveWordAsync());
            CancelCommand = new RelayCommand((param) => Cancel());
        }
        public bool AcceptSuggestion()
        {
            if (!string.IsNullOrWhiteSpace(Suggestion))
            {
                OriginalWord = Suggestion;
                Suggestion = null;
                return true;
            }
            return false;
        }
        private async Task SaveWordAsync()
        {

            if (string.IsNullOrWhiteSpace(OriginalWord) || string.IsNullOrWhiteSpace(Translation))
            {
                return;
            }

            try
            {
                var newWord = new Word
                {
                    DictionaryId = _selectedDictionary.Id,
                    OriginalWord = OriginalWord.Trim(),
                    Translation = Translation.Trim(),
                    Example = Example?.Trim() ?? "",
                    AddedAt = DateTime.Now
                };


                var savedWord = await _dataService.AddWordAsync(newWord);


                EventAggregator.Instance.Publish(new WordAddedMessage(savedWord, savedWord.DictionaryId));

                EventAggregator.Instance.Publish(new EventAggregator.CloseTabMessage(this));

            }
            catch (Exception ex)
            {
            }
        }

        private void Cancel()
        {
            EventAggregator.Instance.Publish(this);
        }

        private void UpdateSuggestion()
        {
            Suggestion = _spellCheckService.SuggestCorrection(OriginalWord);
        }
    }
}
