using LearningTrainer.Core;
using LearningTrainer.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;
using static LearningTrainer.Core.EventAggregator;

namespace LearningTrainer.ViewModels
{
    public class MarketplaceViewModel : TabViewModelBase
    {
        private readonly IDataService _dataService;

        #region Collections

        public ObservableCollection<MarketplaceDictionaryItem> Dictionaries { get; } = new();
        public ObservableCollection<MarketplaceRuleItem> Rules { get; } = new();

        #endregion

        #region Properties - Tab Selection

        private bool _isDictionariesTab = true;
        public bool IsDictionariesTab
        {
            get => _isDictionariesTab;
            set
            {
                if (SetProperty(ref _isDictionariesTab, value) && value)
                {
                    LoadDictionariesAsync();
                }
            }
        }

        private bool _isRulesTab;
        public bool IsRulesTab
        {
            get => _isRulesTab;
            set
            {
                if (SetProperty(ref _isRulesTab, value) && value)
                {
                    LoadRulesAsync();
                }
            }
        }

        #endregion

        #region Properties - Loading & Pagination

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        private int _currentPage = 1;
        public int CurrentPage
        {
            get => _currentPage;
            set
            {
                if (SetProperty(ref _currentPage, value))
                {
                    OnPropertyChanged(nameof(PageInfo));
                }
            }
        }

        private int _totalPages = 1;
        public int TotalPages
        {
            get => _totalPages;
            set
            {
                if (SetProperty(ref _totalPages, value))
                {
                    OnPropertyChanged(nameof(PageInfo));
                    OnPropertyChanged(nameof(CanGoNext));
                    OnPropertyChanged(nameof(CanGoPrevious));
                }
            }
        }

        public string PageInfo => $"Страница {CurrentPage} из {TotalPages}";
        public bool CanGoNext => CurrentPage < TotalPages;
        public bool CanGoPrevious => CurrentPage > 1;

        #endregion

        #region Properties - Filters

        private string _searchQuery = "";
        public string SearchQuery
        {
            get => _searchQuery;
            set => SetProperty(ref _searchQuery, value);
        }

        // Dictionary filters
        private string _selectedLanguageFrom = "";
        public string SelectedLanguageFrom
        {
            get => _selectedLanguageFrom;
            set => SetProperty(ref _selectedLanguageFrom, value);
        }

        private string _selectedLanguageTo = "";
        public string SelectedLanguageTo
        {
            get => _selectedLanguageTo;
            set => SetProperty(ref _selectedLanguageTo, value);
        }

        // Rule filters
        private string _selectedCategory = "";
        public string SelectedCategory
        {
            get => _selectedCategory;
            set => SetProperty(ref _selectedCategory, value);
        }

        private int _selectedDifficulty;
        public int SelectedDifficulty
        {
            get => _selectedDifficulty;
            set => SetProperty(ref _selectedDifficulty, value);
        }

        public List<string> LanguageOptions { get; } = new()
        {
            "", "en", "ru", "de", "es", "fr"
        };

        public List<string> CategoryOptions { get; } = new()
        {
            "", "Grammar", "Vocabulary", "Pronunciation"
        };

        public List<KeyValuePair<int, string>> DifficultyOptions { get; } = new()
        {
            new(0, "Любой"),
            new(1, "Начальный"),
            new(2, "Средний"),
            new(3, "Продвинутый")
        };

        #endregion

        #region Properties - Selected Items

        private MarketplaceDictionaryItem? _selectedDictionary;
        public MarketplaceDictionaryItem? SelectedDictionary
        {
            get => _selectedDictionary;
            set => SetProperty(ref _selectedDictionary, value);
        }

        private MarketplaceRuleItem? _selectedRule;
        public MarketplaceRuleItem? SelectedRule
        {
            get => _selectedRule;
            set => SetProperty(ref _selectedRule, value);
        }

        #endregion

        #region Commands

        public ICommand SearchCommand { get; }
        public ICommand ClearFiltersCommand { get; }
        public ICommand NextPageCommand { get; }
        public ICommand PreviousPageCommand { get; }
        public ICommand ViewDictionaryDetailsCommand { get; }
        public ICommand ViewRuleDetailsCommand { get; }
        public ICommand DownloadDictionaryCommand { get; }
        public ICommand DownloadRuleCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand CloseCommand { get; }

        #endregion

        public MarketplaceViewModel(IDataService dataService)
        {
            _dataService = dataService;
            SetLocalizedTitle("Loc.Tab.Marketplace");

            SearchCommand = new RelayCommand(_ => ExecuteSearch());
            ClearFiltersCommand = new RelayCommand(_ => ClearFilters());
            NextPageCommand = new RelayCommand(_ => GoToNextPage(), _ => CanGoNext);
            PreviousPageCommand = new RelayCommand(_ => GoToPreviousPage(), _ => CanGoPrevious);
            ViewDictionaryDetailsCommand = new RelayCommand(ViewDictionaryDetails);
            ViewRuleDetailsCommand = new RelayCommand(ViewRuleDetails);
            DownloadDictionaryCommand = new RelayCommand(async p => await DownloadDictionary(p));
            DownloadRuleCommand = new RelayCommand(async p => await DownloadRule(p));
            RefreshCommand = new RelayCommand(_ => Refresh());
            CloseCommand = new RelayCommand(_ => Close());

            // Initial load
            LoadDictionariesAsync();
        }

        #region Methods

        private async void LoadDictionariesAsync()
        {
            IsLoading = true;
            try
            {
                var result = await _dataService.GetPublicDictionariesAsync(
                    string.IsNullOrEmpty(SearchQuery) ? null : SearchQuery,
                    string.IsNullOrEmpty(SelectedLanguageFrom) ? null : SelectedLanguageFrom,
                    string.IsNullOrEmpty(SelectedLanguageTo) ? null : SelectedLanguageTo,
                    CurrentPage,
                    9);

                Dictionaries.Clear();
                foreach (var item in result.Items)
                {
                    Dictionaries.Add(item);
                }

                TotalPages = Math.Max(1, result.TotalPages);
                CurrentPage = result.CurrentPage > 0 ? result.CurrentPage : 1;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MARKETPLACE] LoadDictionaries error: {ex.Message}");
                EventAggregator.Instance.Publish(ShowNotificationMessage.Error(
                    "Ошибка", $"Не удалось загрузить словари: {ex.Message}"));
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async void LoadRulesAsync()
        {
            IsLoading = true;
            try
            {
                var result = await _dataService.GetPublicRulesAsync(
                    string.IsNullOrEmpty(SearchQuery) ? null : SearchQuery,
                    string.IsNullOrEmpty(SelectedCategory) ? null : SelectedCategory,
                    SelectedDifficulty,
                    CurrentPage,
                    8);

                Rules.Clear();
                foreach (var item in result.Items)
                {
                    Rules.Add(item);
                }

                TotalPages = Math.Max(1, result.TotalPages);
                CurrentPage = result.CurrentPage > 0 ? result.CurrentPage : 1;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MARKETPLACE] LoadRules error: {ex.Message}");
                EventAggregator.Instance.Publish(ShowNotificationMessage.Error(
                    "Ошибка", $"Не удалось загрузить правила: {ex.Message}"));
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void ExecuteSearch()
        {
            CurrentPage = 1;
            if (IsDictionariesTab)
                LoadDictionariesAsync();
            else
                LoadRulesAsync();
        }

        private void ClearFilters()
        {
            SearchQuery = "";
            SelectedLanguageFrom = "";
            SelectedLanguageTo = "";
            SelectedCategory = "";
            SelectedDifficulty = 0;
            CurrentPage = 1;

            if (IsDictionariesTab)
                LoadDictionariesAsync();
            else
                LoadRulesAsync();
        }

        private void GoToNextPage()
        {
            if (CanGoNext)
            {
                CurrentPage++;
                if (IsDictionariesTab)
                    LoadDictionariesAsync();
                else
                    LoadRulesAsync();
            }
        }

        private void GoToPreviousPage()
        {
            if (CanGoPrevious)
            {
                CurrentPage--;
                if (IsDictionariesTab)
                    LoadDictionariesAsync();
                else
                    LoadRulesAsync();
            }
        }

        private void ViewDictionaryDetails(object? param)
        {
            if (param is MarketplaceDictionaryItem item || (SelectedDictionary != null && (item = SelectedDictionary) != null))
            {
                var detailsVm = new MarketplaceDictionaryDetailsViewModel(_dataService, item.Id);
                EventAggregator.Instance.Publish(detailsVm);
            }
        }

        private void ViewRuleDetails(object? param)
        {
            if (param is MarketplaceRuleItem item || (SelectedRule != null && (item = SelectedRule) != null))
            {
                var detailsVm = new MarketplaceRuleDetailsViewModel(_dataService, item.Id);
                EventAggregator.Instance.Publish(detailsVm);
            }
        }

        private async Task DownloadDictionary(object? param)
        {
            int? id = param switch
            {
                int i => i,
                MarketplaceDictionaryItem item => item.Id,
                _ => SelectedDictionary?.Id
            };

            if (id == null) return;

            var (success, message, newId) = await _dataService.DownloadDictionaryFromMarketplaceAsync(id.Value);
            
            if (success)
            {
                EventAggregator.Instance.Publish(ShowNotificationMessage.Success("Успешно", message));
                EventAggregator.Instance.Publish(new RefreshDataMessage());
            }
            else
            {
                EventAggregator.Instance.Publish(ShowNotificationMessage.Error("Ошибка", message));
            }
        }

        private async Task DownloadRule(object? param)
        {
            int? id = param switch
            {
                int i => i,
                MarketplaceRuleItem item => item.Id,
                _ => SelectedRule?.Id
            };

            if (id == null) return;

            var (success, message, newId) = await _dataService.DownloadRuleFromMarketplaceAsync(id.Value);
            
            if (success)
            {
                EventAggregator.Instance.Publish(ShowNotificationMessage.Success("Успешно", message));
                EventAggregator.Instance.Publish(new RefreshDataMessage());
            }
            else
            {
                EventAggregator.Instance.Publish(ShowNotificationMessage.Error("Ошибка", message));
            }
        }

        private void Refresh()
        {
            if (IsDictionariesTab)
                LoadDictionariesAsync();
            else
                LoadRulesAsync();
        }

        private void Close()
        {
            EventAggregator.Instance.Publish(new CloseTabMessage(this));
        }

        #endregion
    }
}
