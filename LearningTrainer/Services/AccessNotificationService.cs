using System.Collections.ObjectModel;

namespace LearningTrainer.Services
{
    /// <summary>
    /// Сервис для управления уведомлениями о правах доступа
    /// </summary>
    public class AccessNotificationService
    {
        private readonly ObservableCollection<AccessNotification> _notifications;
        private int _notificationId = 0;

        public ObservableCollection<AccessNotification> Notifications => _notifications;

        public event Action<AccessNotification> NotificationAdded;
        public event Action<int> NotificationRemoved;

        public AccessNotificationService()
        {
            _notifications = new ObservableCollection<AccessNotification>();
        }

        /// <summary>
        /// Добавить уведомление об отказе в доступе
        /// </summary>
        public void AddAccessDeniedNotification(string actionName, string userRole, string requiredRole)
        {
            var notification = new AccessNotification
            {
                Id = ++_notificationId,
                Type = NotificationType.AccessDenied,
                Title = "Доступ запрещён",
                Message = $"Действие '{actionName}' недоступно для роли '{userRole}'",
                ActionName = actionName,
                RequiredRole = requiredRole,
                UserRole = userRole,
                Timestamp = DateTime.UtcNow,
                IsRead = false,
                Duration = TimeSpan.FromSeconds(8)
            };

            _notifications.Add(notification);
            NotificationAdded?.Invoke(notification);

            AutoRemoveNotification(notification.Id, notification.Duration);
        }

        /// <summary>
        /// Добавить информационное уведомление
        /// </summary>
        public void AddInfoNotification(string title, string message)
        {
            var notification = new AccessNotification
            {
                Id = ++_notificationId,
                Type = NotificationType.Info,
                Title = title,
                Message = message,
                Timestamp = DateTime.UtcNow,
                IsRead = false,
                Duration = TimeSpan.FromSeconds(6)
            };

            _notifications.Add(notification);
            NotificationAdded?.Invoke(notification);

            AutoRemoveNotification(notification.Id, notification.Duration);
        }

        /// <summary>
        /// Добавить уведомление об успехе
        /// </summary>
        public void AddSuccessNotification(string title, string message)
        {
            var notification = new AccessNotification
            {
                Id = ++_notificationId,
                Type = NotificationType.Success,
                Title = title,
                Message = message,
                Timestamp = DateTime.UtcNow,
                IsRead = false,
                Duration = TimeSpan.FromSeconds(5)
            };

            _notifications.Add(notification);
            NotificationAdded?.Invoke(notification);

            AutoRemoveNotification(notification.Id, notification.Duration);
        }

        /// <summary>
        /// Добавить уведомление об ошибке
        /// </summary>
        public void AddErrorNotification(string title, string message)
        {
            var notification = new AccessNotification
            {
                Id = ++_notificationId,
                Type = NotificationType.Error,
                Title = title,
                Message = message,
                Timestamp = DateTime.UtcNow,
                IsRead = false,
                Duration = TimeSpan.FromSeconds(10)
            };

            _notifications.Add(notification);
            NotificationAdded?.Invoke(notification);

            AutoRemoveNotification(notification.Id, notification.Duration);
        }

        /// <summary>
        /// Добавить уведомление о роли пользователя
        /// </summary>
        public void AddRoleInfoNotification(string username, string roleName, string roleDescription)
        {
            var notification = new AccessNotification
            {
                Id = ++_notificationId,
                Type = NotificationType.RoleInfo,
                Title = "Информация о роли",
                Message = $"{username}\n{roleDescription}",
                UserRole = roleName,
                Timestamp = DateTime.UtcNow,
                IsRead = false,
                Duration = TimeSpan.FromSeconds(7)
            };

            _notifications.Add(notification);
            NotificationAdded?.Invoke(notification);

            AutoRemoveNotification(notification.Id, notification.Duration);
        }

        /// <summary>
        /// Удалить уведомление по ID
        /// </summary>
        public void RemoveNotification(int id)
        {
            var notification = _notifications.FirstOrDefault(n => n.Id == id);
            if (notification != null)
            {
                _notifications.Remove(notification);
                NotificationRemoved?.Invoke(id);
            }
        }

        /// <summary>
        /// Удалить все уведомления
        /// </summary>
        public void ClearAllNotifications()
        {
            var ids = _notifications.Select(n => n.Id).ToList();
            _notifications.Clear();
            foreach (var id in ids)
            {
                NotificationRemoved?.Invoke(id);
            }
        }

        /// <summary>
        /// Отметить уведомление как прочитанное
        /// </summary>
        public void MarkAsRead(int id)
        {
            var notification = _notifications.FirstOrDefault(n => n.Id == id);
            if (notification != null)
            {
                notification.IsRead = true;
            }
        }

        private async void AutoRemoveNotification(int id, TimeSpan duration)
        {
            await Task.Delay(duration);
            RemoveNotification(id);
        }
    }

    /// <summary>
    /// Тип уведомления
    /// </summary>
    public enum NotificationType
    {
        AccessDenied,
        Info,
        Success,
        Error,
        RoleInfo,
        Warning
    }

    /// <summary>
    /// Модель уведомления о доступе
    /// </summary>
    public class AccessNotification
    {
        public int Id { get; set; }
        public NotificationType Type { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        public string ActionName { get; set; }
        public string RequiredRole { get; set; }
        public string UserRole { get; set; }
        public DateTime Timestamp { get; set; }
        public bool IsRead { get; set; }
        public TimeSpan Duration { get; set; } = TimeSpan.FromSeconds(6);

        /// <summary>
        /// Получить цвет для типа уведомления
        /// </summary>
        public string GetTypeColor()
        {
            return Type switch
            {
                NotificationType.AccessDenied => "#F87171", // Red
                NotificationType.Info => "#3B82F6", // Blue
                NotificationType.Success => "#30A966", // Green
                NotificationType.Error => "#EF4444", // Dark Red
                NotificationType.RoleInfo => "#8B5CF6", // Purple
                NotificationType.Warning => "#FBBF24", // Amber
                _ => "#6B7280" // Gray
            };
        }

        /// <summary>
        /// Получить иконку для типа уведомления
        /// </summary>
        public string GetTypeIcon()
        {
            return Type switch
            {
                NotificationType.AccessDenied => "??",
                NotificationType.Info => "?",
                NotificationType.Success => "?",
                NotificationType.Error => "?",
                NotificationType.RoleInfo => "??",
                NotificationType.Warning => "?",
                _ => "•"
            };
        }
    }
}
