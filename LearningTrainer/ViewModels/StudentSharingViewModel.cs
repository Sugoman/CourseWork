using LearningTrainer.Core;
using LearningTrainer.Services;
using LearningTrainerShared.Models; // Убедись, что Enum ShareContentType здесь или добавь нужный using

namespace LearningTrainer.ViewModels
{
    public class StudentSharingViewModel : ObservableObject
    {
        public StudentDto Student { get; }

        private readonly int _entityId;
        private readonly ShareContentType _contentType;
        private readonly IDataService _dataService;

        private bool _isShared;

        public bool IsShared
        {
            get => _isShared;
            set
            {
                if (SetProperty(ref _isShared, value))
                {
                    ToggleSharingAsync(value);
                }
            }
        }

        public StudentSharingViewModel(StudentDto student,
            int entityId,
            ShareContentType type,
            IDataService dataService,
            bool initiallyShared)
        {
            Student = student;
            _entityId = entityId;
            _contentType = type;
            _dataService = dataService;
            _isShared = initiallyShared;
        }

        private async void ToggleSharingAsync(bool share)
        {
            try
            {
                SharingResultDto result;

                if (_contentType == ShareContentType.Dictionary)
                {
                    result = await _dataService.ToggleDictionarySharingAsync(_entityId, Student.Id);
                }
                else
                {
                    result = await _dataService.ToggleRuleSharingAsync(_entityId, Student.Id);
                }

                if ((share && result.Status != "Shared") || (!share && result.Status != "Unshared"))
                {
                    SetProperty(ref _isShared, !share, nameof(IsShared));
                    EventAggregator.Instance.Publish(ShowNotificationMessage.Error(
                        "Ошибка обмена",
                        result.Message ?? "Статус не совпал"));
                }
            }
            catch (System.Exception ex)
            {
                SetProperty(ref _isShared, !share, nameof(IsShared));
                EventAggregator.Instance.Publish(ShowNotificationMessage.Error(
                    "Ошибка связи",
                    $"Ошибка API: {ex.Message}"));
            }
        }
    }
}