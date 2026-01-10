namespace LearningTrainerShared.Constants
{
    /// <summary>
    /// Константы для ролей пользователей
    /// </summary>
    public static class UserRoles
    {
        public const string Admin = "Admin";
        public const string Teacher = "Teacher";
        public const string Student = "Student";
        public const string User = "User";

        public static readonly string[] AllRoles = { Admin, Teacher, Student, User };
        
        /// <summary>
        /// Роли с полным доступом к созданию контента (Admin, User, Teacher)
        /// </summary>
        public const string ContentCreators = $"{Admin},{User},{Teacher}";
        
        /// <summary>
        /// Роли с доступом к админ-панели (только Admin)
        /// </summary>
        public const string AdminPanelAccess = Admin;
    }
}
