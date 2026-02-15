using LearningTrainer.Core;
using LearningTrainer.Services;
using LearningTrainerShared.Models;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using static LearningTrainer.Core.EventAggregator;

namespace LearningTrainer.ViewModels
{
    public class MarketplaceRuleDetailsViewModel : TabViewModelBase
    {
        private readonly IDataService _dataService;
        private readonly int _ruleId;

        #region Properties

        private bool _isLoading = true;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        private MarketplaceRuleDetails? _rule;
        public MarketplaceRuleDetails? Rule
        {
            get => _rule;
            set
            {
                if (SetProperty(ref _rule, value))
                {
                    OnPropertyChanged(nameof(HasRule));
                    OnPropertyChanged(nameof(RatingStars));
                    OnPropertyChanged(nameof(DifficultyText));
                }
            }
        }

        public bool HasRule => Rule != null;

        public string RatingStars => Rule != null
            ? new string('★', (int)Math.Round(Rule.Rating)) + new string('☆', 5 - (int)Math.Round(Rule.Rating))
            : "☆☆☆☆☆";

        public string DifficultyText => Rule?.DifficultyLevel switch
        {
            1 => "🟢 Начальный",
            2 => "🟡 Средний",
            3 => "🔴 Продвинутый",
            _ => "Не указан"
        };

        public ObservableCollection<CommentItem> Comments { get; } = new();
        public ObservableCollection<MarketplaceRuleItem> RelatedRules { get; } = new();

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
        public bool HasRelatedRules => RelatedRules.Count > 0;

        private MarkdownConfig _config;
        public MarkdownConfig Config
        {
            get => _config;
            set => SetProperty(ref _config, value);
        }

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
        public ICommand ViewRelatedRuleCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand CloseCommand { get; }

        #endregion

        public MarketplaceRuleDetailsViewModel(IDataService dataService, int ruleId)
        {
            _dataService = dataService;
            _ruleId = ruleId;

            SetLocalizedTitle("Loc.Tab.RuleDetails");

            DownloadCommand = new RelayCommand(async _ => await DownloadRule());
            SubmitCommentCommand = new RelayCommand(async _ => await SubmitComment(), _ => !IsSubmittingComment);
            ViewRelatedRuleCommand = new RelayCommand(ViewRelatedRule);
            RefreshCommand = new RelayCommand(_ => LoadData());
            CloseCommand = new RelayCommand(_ => Close());

            Config = GetConfigFromCurrentTheme();
            LoadData();
        }

        private async void LoadData()
        {
            IsLoading = true;
            try
            {
                // Load rule details
                Rule = await _dataService.GetMarketplaceRuleDetailsAsync(_ruleId);

                if (Rule != null)
                {
                    TitleSuffix = $": {Rule.Title}";
                    UpdateLocalizedTitle();

                    // Load related rules
                    if (!string.IsNullOrEmpty(Rule.Category))
                    {
                        var related = await _dataService.GetRelatedRulesAsync(_ruleId, Rule.Category);
                        RelatedRules.Clear();
                        foreach (var item in related)
                        {
                            RelatedRules.Add(item);
                        }
                    }
                }

                // Load comments
                var comments = await _dataService.GetRuleCommentsAsync(_ruleId);
                Comments.Clear();
                foreach (var comment in comments)
                {
                    Comments.Add(comment);
                }

                // Check if current user already left a review
                HasUserReview = HasUserReview || await _dataService.HasUserReviewedRuleAsync(_ruleId);
                OnPropertyChanged(nameof(HasRelatedRules));
            }
            catch (Exception ex)
            {
                EventAggregator.Instance.Publish(ShowNotificationMessage.Error(
                    "Ошибка", $"Не удалось загрузить данные: {ex.Message}"));
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task DownloadRule()
        {
            var (success, message, newId) = await _dataService.DownloadRuleFromMarketplaceAsync(_ruleId);
            
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
                var success = await _dataService.AddRuleCommentAsync(_ruleId, NewRating, NewCommentText);
                
                if (success)
                {
                    EventAggregator.Instance.Publish(ShowNotificationMessage.Success("Успешно", "Комментарий добавлен"));
                    NewCommentText = "";
                    NewRating = 5;
                    HasUserReview = true;

                    // Reload comments
                    var comments = await _dataService.GetRuleCommentsAsync(_ruleId);
                    Comments.Clear();
                    foreach (var comment in comments)
                    {
                        Comments.Add(comment);
                    }

                    // Reload rule to get updated rating
                    Rule = await _dataService.GetMarketplaceRuleDetailsAsync(_ruleId);
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

        private void ViewRelatedRule(object? param)
        {
            if (param is MarketplaceRuleItem item)
            {
                var detailsVm = new MarketplaceRuleDetailsViewModel(_dataService, item.Id);
                EventAggregator.Instance.Publish(detailsVm);
            }
        }

        private void Close()
        {
            EventAggregator.Instance.Publish(new CloseTabMessage(this));
        }

        private static MarkdownConfig GetConfigFromCurrentTheme()
        {
            string GetColor(string resourceKey, string fallbackColor)
            {
                if (Application.Current.Resources[resourceKey] is SolidColorBrush brush)
                    return $"#{brush.Color.R:X2}{brush.Color.G:X2}{brush.Color.B:X2}";
                return fallbackColor;
            }

            return new MarkdownConfig
            {
                BackgroundColor = GetColor("CardBackgroundBrush", "#FFFFFF"),
                TextColor = GetColor("PrimaryTextBrush", "#000000"),
                AccentColor = GetColor("PrimaryAccentBrush", "#0056b3"),
                FontSize = 14
            };
        }
    }
}
