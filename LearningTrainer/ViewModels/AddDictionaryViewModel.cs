using LearningTrainer.Core;
using LearningTrainer.Services;
using LearningTrainerShared.Models;
using System.Windows.Input;

namespace LearningTrainer.ViewModels
{
    public class AddDictionaryViewModel : TabViewModelBase
    {
        private readonly IDataService _dataService;

        public string DictionaryName { get; set; }
        public string Description { get; set; }
        public string LanguageFrom { get; set; } = "English";
        public string LanguageTo { get; set; } = "Russian";

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }

        public AddDictionaryViewModel(IDataService dataService)
        {
            _dataService = dataService;
            SetLocalizedTitle("Loc.Tab.CreateDictionary");

            SaveCommand = new RelayCommand(async (param) => await SaveDictionaryAsync());
            CancelCommand = new RelayCommand((param) => Cancel());
        }

        private async Task SaveDictionaryAsync()
        {
            if (string.IsNullOrWhiteSpace(DictionaryName))
            {
                return;
            }

            try
            {
                var newDictionary = new Dictionary
                {
                    Name = DictionaryName.Trim(),
                    Description = Description?.Trim() ?? "",
                    LanguageFrom = LanguageFrom.Trim(),
                    LanguageTo = LanguageTo.Trim()
                };

                var savedDictionary = await _dataService.AddDictionaryAsync(newDictionary);

                EventAggregator.Instance.Publish(new DictionaryAddedMessage(savedDictionary));

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
    }
}
