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

        public static readonly string[] AllRoles = { Admin, Teacher, Student };
    }
}
