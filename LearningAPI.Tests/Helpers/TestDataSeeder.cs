using LearningTrainerShared.Models;
using BCrypt.Net;

namespace LearningAPI.Tests.Helpers;

public static class TestDataSeeder
{
    public static Role CreateTeacherRole() => new Role { Id = 1, Name = "Teacher" };
    public static Role CreateStudentRole() => new Role { Id = 2, Name = "Student" };
    public static Role CreateAdminRole() => new Role { Id = 3, Name = "Admin" };

    public static User CreateTestUser(string login = "testuser", string password = "password123", Role? role = null)
    {
        return new User
        {
            Id = 0,
            Login = login,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Role = role ?? CreateTeacherRole(),
            InviteCode = $"TR-TEST01"
        };
    }

    public static Dictionary CreateTestDictionary(int userId, string name = "Test Dictionary")
    {
        return new Dictionary
        {
            Id = 0,
            Name = name,
            Description = "Test description",
            LanguageFrom = "English",
            LanguageTo = "Russian",
            UserId = userId,
            Words = new List<Word>()
        };
    }

    public static Word CreateTestWord(int dictionaryId, int userId)
    {
        return new Word
        {
            Id = 0,
            OriginalWord = "Hello",
            Translation = "Привет",
            Transcription = "[h??lo?]",
            Example = "Hello, world!",
            DictionaryId = dictionaryId,
            UserId = userId,
            AddedAt = DateTime.UtcNow
        };
    }

    public static Rule CreateTestRule(int userId, string title = "Test Rule")
    {
        return new Rule
        {
            Id = 0,
            Title = title,
            Description = "Test rule description",
            MarkdownContent = "# Test Rule\n\nThis is a test.",
            UserId = userId
        };
    }
}
