using LearningTrainer.Core;
using LearningTrainer.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;
using static LearningTrainer.Core.EventAggregator;

namespace LearningTrainer.ViewModels
{
    public class MarketplaceDictionaryDetailsViewModel : TabViewModelBase
    {
        private readonly IDataService _dataService;
        private readonly int _dictionaryId;

        #region Properties

        private bool _isLoading = true;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        private MarketplaceDictionaryDetails? _dictionary;
        public MarketplaceDictionaryDetails? Dictionary
        {
            get => _dictionary;
            set
            {
                if (SetProperty(ref _dictionary, value))
                {
                    OnPropertyChanged(nameof(HasDictionary));
                    OnPropertyChanged(nameof(RatingStars));
                }
            }
        }

        public bool HasDictionary => Dictionary != null;

        public string RatingStars => Dictionary != null
            ? new string('★', (int)Math.Round(Dictionary.Rating)) + new string('☆', 5 - (int)Math.Round(Dictionary.Rating))
            : "☆☆☆☆☆";

        public ObservableCollection<CommentItem> Comments { get; } = new();
        public ObservableCollection<WordPreview> PreviewWords { get; } = new();

        // New comment form
        private int _newRating = 5;
        public int NewRating
        {
            get => _newRating;
            set => SetProperty(ref _newRating, value);
        }

        private string _newCommentText = "";
        public string NewCommentText
        {
            get => _newCommentText;
            set => SetProperty(ref _newCommentText, value);
        }

        private bool _isSubmittingComment;
        public bool IsSubmittingComment
        {
            get => _isSubmittingComment;
            set => SetProperty(ref _isSubmittingComment, value);
        }

        private bool _hasUserReview;
        public bool HasUserReview
        {
            get => _hasUserReview;
            set
            {
                if (SetProperty(ref _hasUserReview, value))
                    OnPropertyChanged(nameof(CanAddComment));
            }
        }

        public bool CanAddComment => !HasUserReview;

        public List<KeyValuePair<int, string>> RatingOptions { get; } = new()
        {
            new(5, "★★★★★ Отлично"),
            new(4, "★★★★☆ Хорошо"),
            new(3, "★★★☆☆ Нормально"),
            new(2, "★★☆☆☆ Плохо"),
            new(1, "★☆☆☆☆ Ужасно")
        };

        #endregion

        #region Commands

        public ICommand DownloadCommand { get; }
        public ICommand SubmitCommentCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand CloseCommand { get; }

        #endregion

        public MarketplaceDictionaryDetailsViewModel(IDataService dataService, int dictionaryId)
        {
            _dataService = dataService;
            _dictionaryId = dictionaryId;

            SetLocalizedTitle("Loc.Tab.DictionaryDetails");

            DownloadCommand = new RelayCommand(async _ => await DownloadDictionary());
            SubmitCommentCommand = new RelayCommand(async _ => await SubmitComment(), _ => !IsSubmittingComment);
            RefreshCommand = new RelayCommand(_ => LoadData());
            CloseCommand = new RelayCommand(_ => Close());

            LoadData();
        }

        private async void LoadData()
        {
            IsLoading = true;
            try
            {
                // Load dictionary details
                Dictionary = await _dataService.GetMarketplaceDictionaryDetailsAsync(_dictionaryId);

                if (Dictionary != null)
                {
                    TitleSuffix = $": {Dictionary.Name}";
                    UpdateLocalizedTitle();

                    // Load preview words
                    PreviewWords.Clear();
                    foreach (var word in Dictionary.PreviewWords)
                    {
                        PreviewWords.Add(word);
                    }
                }

                // Load comments
                var comments = await _dataService.GetDictionaryCommentsAsync(_dictionaryId);
                Comments.Clear();
                foreach (var comment in comments)
                {
                    Comments.Add(comment);
                }

                // Check if current user already left a review
                HasUserReview = await _dataService.HasUserReviewedDictionaryAsync(_dictionaryId);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DETAILS] Load error: {ex.Message}");
                EventAggregator.Instance.Publish(ShowNotificationMessage.Error(
                    "Ошибка", $"Не удалось загрузить данные: {ex.Message}"));
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task DownloadDictionary()
        {
            var (success, message, newId) = await _dataService.DownloadDictionaryFromMarketplaceAsync(_dictionaryId);
            
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

        private async Task SubmitComment()
        {
            if (NewRating < 1 || NewRating > 5)
            {
                EventAggregator.Instance.Publish(ShowNotificationMessage.Error("Ошибка", "Выберите оценку от 1 до 5"));
                return;
            }

            IsSubmittingComment = true;
            try
            {
                var success = await _dataService.AddDictionaryCommentAsync(_dictionaryId, NewRating, NewCommentText);
                
                if (success)
                {
                    EventAggregator.Instance.Publish(ShowNotificationMessage.Success("Успешно", "Комментарий добавлен"));
                    NewCommentText = "";
                    NewRating = 5;
                    HasUserReview = true;

                    // Reload comments
                    var comments = await _dataService.GetDictionaryCommentsAsync(_dictionaryId);
                    Comments.Clear();
                    foreach (var comment in comments)
                    {
                        Comments.Add(comment);
                    }

                    // Reload dictionary to get updated rating
                    Dictionary = await _dataService.GetMarketplaceDictionaryDetailsAsync(_dictionaryId);
                }
                else
                {
                    EventAggregator.Instance.Publish(ShowNotificationMessage.Error("Ошибка", "Не удалось добавить комментарий"));
                }
            }
            catch (Exception ex)
            {
                EventAggregator.Instance.Publish(ShowNotificationMessage.Error("Ошибка", ex.Message));
            }
            finally
            {
                IsSubmittingComment = false;
            }
        }

        private void Close()
        {
            EventAggregator.Instance.Publish(new CloseTabMessage(this));
        }
    }
}
