using LearningTrainerShared.Models;

namespace LearningTrainer.Services
{
    /// <summary>
    /// —ервис дл€ управлени€ правами доступа и уведомлений
    /// </summary>
    public class PermissionService
    {
        private readonly User _currentUser;

        public PermissionService(User currentUser)
        {
            _currentUser = currentUser;
        }

        /// <summary>
        /// ѕроверить может ли пользователь создавать словари
        /// </summary>
        public bool CanCreateDictionary => IsTeacherOrAdmin();

        /// <summary>
        /// ѕроверить может ли пользователь создавать правила
        /// </summary>
        public bool CanCreateRules => IsTeacherOrAdmin();

        /// <summary>
        /// ѕроверить может ли пользователь делитьс€ словар€ми
        /// </summary>
        public bool CanShareDictionaries => IsTeacherOrAdmin();

        /// <summary>
        /// ѕроверить может ли пользователь делитьс€ правилами
        /// </summary>
        public bool CanShareRules => IsTeacherOrAdmin();

        /// <summary>
        /// ѕроверить может ли пользователь редактировать словари
        /// </summary>
        public bool CanEditDictionaries => IsTeacherOrAdmin();

        /// <summary>
        /// ѕроверить может ли пользователь редактировать правила
        /// </summary>
        public bool CanEditRules => IsTeacherOrAdmin();

        /// <summary>
        /// ѕроверить может ли пользователь управл€ть пользовател€ми (Admin только)
        /// </summary>
        public bool CanManageUsers => IsAdmin();

        /// <summary>
        /// ѕроверить может ли пользователь просматривать общие словари
        /// </summary>
        public bool CanViewSharedDictionaries => true; // ¬се могут просматривать

        /// <summary>
        /// ѕолучить сообщение об отсутствии прав доступа
        /// </summary>
        public string GetAccessDeniedMessage(string actionName)
        {
            return $"ƒействие '{actionName}' доступно только дл€ {GetRoleDescriptionString()}.";
        }

        /// <summary>
        /// ѕолучить описание текущей роли
        /// </summary>
        public string GetRoleDescription()
        {
            return GetUserRoleDescription(_currentUser.Role?.Name);
        }

        /// <summary>
        /// ѕолучить статус с полной информацией о правах
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
        /// ѕолучить уведомление о попытке несанкционированного действи€
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
            return role == "Teacher" || role == "Admin" || role == "User";
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
                "User" => "пользователей",
                "Student" => "студентов",
                _ => "пользователей"
            };
        }

        private static string GetUserRoleDescription(string roleName)
        {
            return roleName switch
            {
                "Admin" => "?? јдминистратор - полный доступ ко всем функци€м",
                "Teacher" => "????? ”читель - может создавать и делитьс€ материалами",
                "User" => "?? ѕользователь - может создавать и редактировать материалы",
                "Student" => "???? —тудент - может только изучать материалы",
                _ => "? Ќеизвестна€ роль"
            };
        }

        private static string GetRequiredRole(string actionType)
        {
            return actionType switch
            {
                "CreateDictionary" => "Teacher, Admin, User",
                "CreateRule" => "Teacher, Admin, User",
                "ShareDictionary" => "Teacher",
                "ShareRule" => "Teacher",
                "EditDictionary" => "Teacher, Admin, User",
                "EditRule" => "Teacher, Admin, User",
                "ManageUsers" => "Admin",
                _ => "Teacher, Admin, User"
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
    /// —татус прав доступа пользовател€
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
    /// ”ведомление об отказе в доступе
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
                   $"¬аша роль: {UserRole}\n" +
                   $"“ребуетс€ роль: {RequiredRole}";
        }
    }
}
