using LearningTrainerShared.Models;

namespace LearningTrainer.Services
{
    /// <summary>
    /// Сервис для управления правами доступа и уведомлений
    /// </summary>
    public class PermissionService
    {
        private readonly User _currentUser;

        public PermissionService(User currentUser)
        {
            _currentUser = currentUser;
        }

        /// <summary>
        /// Проверить может ли пользователь создавать словари
        /// </summary>
        public bool CanCreateDictionary => IsTeacherOrAdmin();

        /// <summary>
        /// Проверить может ли пользователь создавать правила
        /// </summary>
        public bool CanCreateRules => IsTeacherOrAdmin();

        /// <summary>
        /// Проверить может ли пользователь делиться словарями
        /// </summary>
        public bool CanShareDictionaries => IsTeacherOrAdmin();

        /// <summary>
        /// Проверить может ли пользователь делиться правилами
        /// </summary>
        public bool CanShareRules => IsTeacherOrAdmin();

        /// <summary>
        /// Проверить может ли пользователь редактировать словари
        /// </summary>
        public bool CanEditDictionaries => IsTeacherOrAdmin();

        /// <summary>
        /// Проверить может ли пользователь редактировать правила
        /// </summary>
        public bool CanEditRules => IsTeacherOrAdmin();

        /// <summary>
        /// Проверить может ли пользователь управлять пользователями (Admin только)
        /// </summary>
        public bool CanManageUsers => IsAdmin();

        /// <summary>
        /// Проверить может ли пользователь просматривать общие словари
        /// </summary>
        public bool CanViewSharedDictionaries => true; // Все могут просматривать

        /// <summary>
        /// Получить сообщение об отсутствии прав доступа
        /// </summary>
        public string GetAccessDeniedMessage(string actionName)
        {
            return $"Действие '{actionName}' доступно только для {GetRoleDescriptionString()}.";
        }

        /// <summary>
        /// Получить описание текущей роли
        /// </summary>
        public string GetRoleDescription()
        {
            return GetUserRoleDescription(_currentUser.Role?.Name);
        }

        /// <summary>
        /// Получить статус с полной информацией о правах
        /// </summary>
        public PermissionStatus GetPermissionStatus()
        {
            var roleName = _currentUser.Role?.Name ?? "Unknown";
            
            return new PermissionStatus
            {
                UserId = _currentUser.Id,
                Username = _currentUser.Login,
                RoleName = roleName,
                RoleDescription = GetUserRoleDescription(roleName),
                CanCreateDictionary = CanCreateDictionary,
                CanCreateRules = CanCreateRules,
                CanShareDictionaries = CanShareDictionaries,
                CanShareRules = CanShareRules,
                CanEditDictionaries = CanEditDictionaries,
                CanEditRules = CanEditRules,
                CanManageUsers = CanManageUsers,
                CanViewSharedDictionaries = CanViewSharedDictionaries,
                TotalPermissions = CountTruePermissions()
            };
        }

        /// <summary>
        /// Получить уведомление о попытке несанкционированного действия
        /// </summary>
        public AccessDeniedNotification GetAccessDeniedNotification(string actionName, string actionType)
        {
            return new AccessDeniedNotification
            {
                Timestamp = DateTime.UtcNow,
                ActionName = actionName,
                ActionType = actionType,
                UserRole = _currentUser.Role?.Name ?? "Unknown",
                Message = GetAccessDeniedMessage(actionName),
                RequiredRole = GetRequiredRole(actionType),
                UserLogin = _currentUser.Login
            };
        }

        // ============ Private Methods ============

        private bool IsTeacherOrAdmin()
        {
            var role = _currentUser.Role?.Name ?? "";
            return role == "Teacher" || role == "Admin";
        }

        private bool IsAdmin()
        {
            return _currentUser.Role?.Name == "Admin";
        }

        private string GetRoleDescriptionString()
        {
            var role = _currentUser.Role?.Name ?? "Unknown";
            return role switch
            {
                "Admin" => "администраторов",
                "Teacher" => "учителей и администраторов",
                "Student" => "студентов",
                _ => "пользователей"
            };
        }

        private static string GetUserRoleDescription(string roleName)
        {
            return roleName switch
            {
                "Admin" => "?? Администратор - полный доступ ко всем функциям",
                "Teacher" => "????? Учитель - может создавать и делиться материалами",
                "Student" => "????? Студент - может только изучать материалы",
                _ => "? Неизвестная роль"
            };
        }

        private static string GetRequiredRole(string actionType)
        {
            return actionType switch
            {
                "CreateDictionary" => "Teacher, Admin",
                "CreateRule" => "Teacher, Admin",
                "ShareDictionary" => "Teacher, Admin",
                "ShareRule" => "Teacher, Admin",
                "EditDictionary" => "Teacher, Admin",
                "EditRule" => "Teacher, Admin",
                "ManageUsers" => "Admin",
                _ => "Teacher, Admin"
            };
        }

        private int CountTruePermissions()
        {
            int count = 0;
            if (CanCreateDictionary) count++;
            if (CanCreateRules) count++;
            if (CanShareDictionaries) count++;
            if (CanShareRules) count++;
            if (CanEditDictionaries) count++;
            if (CanEditRules) count++;
            if (CanManageUsers) count++;
            if (CanViewSharedDictionaries) count++;
            return count;
        }
    }

    /// <summary>
    /// Статус прав доступа пользователя
    /// </summary>
    public class PermissionStatus
    {
        public int UserId { get; set; }
        public string Username { get; set; }
        public string RoleName { get; set; }
        public string RoleDescription { get; set; }
        public bool CanCreateDictionary { get; set; }
        public bool CanCreateRules { get; set; }
        public bool CanShareDictionaries { get; set; }
        public bool CanShareRules { get; set; }
        public bool CanEditDictionaries { get; set; }
        public bool CanEditRules { get; set; }
        public bool CanManageUsers { get; set; }
        public bool CanViewSharedDictionaries { get; set; }
        public int TotalPermissions { get; set; }
    }

    /// <summary>
    /// Уведомление об отказе в доступе
    /// </summary>
    public class AccessDeniedNotification
    {
        public DateTime Timestamp { get; set; }
        public string ActionName { get; set; }
        public string ActionType { get; set; }
        public string UserRole { get; set; }
        public string Message { get; set; }
        public string RequiredRole { get; set; }
        public string UserLogin { get; set; }

        public string GetFormattedMessage()
        {
            return $"[{Timestamp:HH:mm:ss}] {Message}\n" +
                   $"Ваша роль: {UserRole}\n" +
                   $"Требуется роль: {RequiredRole}";
        }
    }
}
