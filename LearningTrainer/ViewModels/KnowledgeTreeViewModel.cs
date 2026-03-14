using LearningTrainer.Core;
using LearningTrainer.Services;
using LearningTrainerShared.Models.KnowledgeTreeDto;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace LearningTrainer.ViewModels
{
    public class KnowledgeTreeViewModel : TabViewModelBase
    {
        private readonly IDataService _dataService;
        private bool _isLoading;
        private KnowledgeTreeState? _treeState;
        private ObservableCollection<TreeSkinInfo> _skins = new();

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public KnowledgeTreeState? TreeState
        {
            get => _treeState;
            set
            {
                if (SetProperty(ref _treeState, value))
                {
                    OnPropertyChanged(nameof(HasData));
                    OnPropertyChanged(nameof(StageEmoji));
                    OnPropertyChanged(nameof(StageName));
                    OnPropertyChanged(nameof(ProgressPercent));
                    OnPropertyChanged(nameof(ProgressText));
                    OnPropertyChanged(nameof(StatsText));
                    OnPropertyChanged(nameof(IsWilting));
                    OnPropertyChanged(nameof(WiltingMessage));
                }
            }
        }

        public ObservableCollection<TreeSkinInfo> Skins
        {
            get => _skins;
            set => SetProperty(ref _skins, value);
        }

        public bool HasData => TreeState != null;
        public string StageEmoji => TreeState?.StageEmoji ?? "🌰";
        public string StageName => TreeState?.StageName ?? "Загрузка...";
        public double ProgressPercent => TreeState?.ProgressToNextStage ?? 0;
        public string ProgressText => TreeState != null
            ? $"{TreeState.ProgressToNextStage:F0}% до следующей стадии"
            : "";
        public string StatsText => TreeState != null
            ? $"{TreeState.TotalWordsContributed} слов · {TreeState.TotalXpContributed} XP"
            : "";
        public bool IsWilting => TreeState?.IsWilting ?? false;
        public string WiltingMessage => TreeState != null && TreeState.IsWilting
            ? $"Дерево увядает! Не занимались {TreeState.DaysSinceActivity} дней."
            : "";

        public ICommand RefreshCommand { get; }
        public ICommand ChangeSkinCommand { get; }

        public KnowledgeTreeViewModel(IDataService dataService)
        {
            _dataService = dataService;
            SetLocalizedTitle("Loc.Tab.KnowledgeTree", "🌳");

            RefreshCommand = new RelayCommand(_ => _ = LoadDataAsync());
            ChangeSkinCommand = new RelayCommand(async param =>
            {
                if (param is int skinId)
                {
                    var success = await _dataService.ChangeTreeSkinAsync(skinId);
                    if (success)
                        await LoadDataAsync();
                }
            });

            _ = LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            IsLoading = true;
            try
            {
                var treeTask = _dataService.GetKnowledgeTreeStateAsync();
                var skinsTask = _dataService.GetTreeSkinsAsync();
                await Task.WhenAll(treeTask, skinsTask);

                TreeState = treeTask.Result;
                Skins = new ObservableCollection<TreeSkinInfo>(skinsTask.Result);
            }
            catch
            {
                TreeState = null;
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}
