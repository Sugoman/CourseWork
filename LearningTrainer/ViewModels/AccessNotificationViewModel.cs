using LearningTrainer.Core;
using LearningTrainer.Services;
using LearningTrainerShared.Models;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace LearningTrainer.ViewModels
{
    /// <summary>
    /// ViewModel дл€ управлени€ уведомлени€ми о правах доступа
    /// </summary>
    public class AccessNotificationViewModel : ObservableObject
    {
        private readonly AccessNotificationService _notificationService;
        private readonly PermissionService _permissionService;
        private readonly User _currentUser;

        private int _totalNotifications;
        private int _unreadNotifications;
        private bool _hasNotifications;
        private AccessNotification _lastNotification;

        public ObservableCollection<AccessNotification> Notifications =>
            _notificationService.Notifications;

        public int TotalNotifications
        {
            get => _totalNotifications;
            set => SetProperty(ref _totalNotifications, value);
        }

        public int UnreadNotifications
        {
            get => _unreadNotifications;
            set => SetProperty(ref _unreadNotifications, value);
        }

        public bool HasNotifications
        {
            get => _hasNotifications;
            set => SetProperty(ref _hasNotifications, value);
        }

        public AccessNotification LastNotification
        {
            get => _lastNotification;
            set => SetProperty(ref _lastNotification, value);
        }

        public ICommand ClearAllNotificationsCommand { get; }
        public ICommand RemoveNotificationCommand { get; }
        public ICommand MarkAsReadCommand { get; }
        public ICommand ShowPermissionStatusCommand { get; }

        public AccessNotificationViewModel(User currentUser, AccessNotificationService notificationService)
        {
            _currentUser = currentUser;
            _notificationService = notificationService;
            _permissionService = new PermissionService(currentUser);

            ClearAllNotificationsCommand = new RelayCommand(_ => ClearAllNotifications());
            RemoveNotificationCommand = new RelayCommand(param => 
            {
                if (param is int id)
                    RemoveNotification(id);
            });
            MarkAsReadCommand = new RelayCommand(param =>
            {
                if (param is int id)
                    MarkAsRead(id);
            });
            ShowPermissionStatusCommand = new RelayCommand(_ => ShowPermissionStatus());

            _notificationService.NotificationAdded += OnNotificationAdded;
            _notificationService.NotificationRemoved += OnNotificationRemoved;

            UpdateNotificationCounts();
        }

        /// <summary>
        /// ѕоказать отказ в доступе дл€ действи€
        /// </summary>
        public void ShowAccessDenied(string actionName, string actionType)
        {
            var requiredRole = _permissionService.GetAccessDeniedNotification(actionName, actionType).RequiredRole;
            _notificationService.AddAccessDeniedNotification(
                actionName,
                _permissionService.GetRoleDescription(),
                requiredRole
            );
        }

        /// <summary>
        /// ѕоказать статус прав доступа
        /// </summary>
        public void ShowPermissionStatus()
        {
            var status = _permissionService.GetPermissionStatus();
            
            string message = $"?? —татистика прав доступа:\n\n" +
                           $"ƒоступные действи€: {status.TotalPermissions}/8\n" +
                           $"Х —оздавать словари: {(status.CanCreateDictionary ? "?" : "?")}\n" +
                           $"Х —оздавать правила: {(status.CanCreateRules ? "?" : "?")}\n" +
                           $"Х ƒелитьс€ словар€ми: {(status.CanShareDictionaries ? "?" : "?")}\n" +
                           $"Х ƒелитьс€ правилами: {(status.CanShareRules ? "?" : "?")}\n" +
                           $"Х –едактировать словари: {(status.CanEditDictionaries ? "?" : "?")}\n" +
                           $"Х –едактировать правила: {(status.CanEditRules ? "?" : "?")}\n" +
                           $"Х ”правл€ть пользовател€ми: {(status.CanManageUsers ? "?" : "?")}\n" +
                           $"Х ѕросматривать общие материалы: {(status.CanViewSharedDictionaries ? "?" : "?")}";

            _notificationService.AddRoleInfoNotification(
                _currentUser.Login,
                status.RoleName,
                message
            );
        }

        /// <summary>
        /// ѕроверить действие и показать уведомление если нет прав
        /// </summary>
        public bool CheckPermissionAndNotify(string actionName, bool hasPermission, string actionType = "")
        {
            if (!hasPermission)
            {
                ShowAccessDenied(actionName, actionType);
                return false;
            }
            return true;
        }

        private void OnNotificationAdded(AccessNotification notification)
        {
            LastNotification = notification;
            UpdateNotificationCounts();
        }

        private void OnNotificationRemoved(int id)
        {
            UpdateNotificationCounts();
        }

        private void ClearAllNotifications()
        {
            _notificationService.ClearAllNotifications();
            UpdateNotificationCounts();
        }

        private void RemoveNotification(int id)
        {
            _notificationService.RemoveNotification(id);
            UpdateNotificationCounts();
        }

        private void MarkAsRead(int id)
        {
            _notificationService.MarkAsRead(id);
            UpdateNotificationCounts();
        }

        private void UpdateNotificationCounts()
        {
            TotalNotifications = _notificationService.Notifications.Count;
            UnreadNotifications = _notificationService.Notifications.Count(n => !n.IsRead);
            HasNotifications = TotalNotifications > 0;
        }
    }
}
