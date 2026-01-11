using LearningTrainer.Core;
using LearningTrainer.Services;
using LearningTrainerShared.Models;
using System.Collections.ObjectModel;
using System.Windows.Input;
using static LearningTrainer.Core.EventAggregator;

namespace LearningTrainer.ViewModels
{
    public class ShareContentViewModel : TabViewModelBase
    {
        private readonly IDataService _dataService;
        private readonly int _entityId;
        private readonly ShareContentType _contentType; // Тип контента

        public ObservableCollection<StudentSharingViewModel> Students { get; set; } = new ObservableCollection<StudentSharingViewModel>();

        private bool _isLoading;
        public bool IsLoading { get => _isLoading; set => SetProperty(ref _isLoading, value); }

        public ICommand CloseCommand { get; }

        public ShareContentViewModel(IDataService dataService, int entityId, string title, ShareContentType type)
        {
            _dataService = dataService;
            _entityId = entityId;
            _contentType = type;
            SetLocalizedTitle("Loc.Tab.ShareAccess", $": {title}");

            CloseCommand = new RelayCommand(_ => EventAggregator.Instance.Publish(new CloseTabMessage(this)));
            LoadSharingDataAsync();
        }

        private async void LoadSharingDataAsync()
        {
            IsLoading = true;
            try
            {
                var allStudents = await _dataService.GetMyStudentsAsync();
                List<int> sharedIds;

                if (_contentType == ShareContentType.Dictionary)
                    sharedIds = await _dataService.GetDictionarySharingStatusAsync(_entityId);
                else
                    sharedIds = await _dataService.GetRuleSharingStatusAsync(_entityId);

                Students.Clear();
                foreach (var student in allStudents)
                {
                    bool isShared = sharedIds.Contains(student.Id);

                    Students.Add(new StudentSharingViewModel(
                        student,       
                        _entityId,     
                        _contentType,  
                        _dataService,  
                        isShared       
                    ));
                }
            }
            catch (Exception ex)
            {
                EventAggregator.Instance.Publish(ShowNotificationMessage.Error(
                    "Ошибка",
                    ex.Message));
            }
            finally { IsLoading = false; }
        }
    }
    public enum ShareContentType { Dictionary, Rule }
}