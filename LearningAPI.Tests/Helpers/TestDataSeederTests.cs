using FluentAssertions;
using LearningAPI.Tests.Helpers;
using LearningTrainerShared.Models;
using Xunit;

namespace LearningAPI.Tests.Helpers;

public class TestDataSeederTests
{
    #region CreateTeacherRole Tests

    [Fact]
    public void CreateTeacherRole_ReturnsRoleWithCorrectProperties()
    {
        // Act
        var role = TestDataSeeder.CreateTeacherRole();

        // Assert
        role.Should().NotBeNull();
        role.Id.Should().Be(1);
        role.Name.Should().Be("Teacher");
    }

    #endregion

    #region CreateStudentRole Tests

    [Fact]
    public void CreateStudentRole_ReturnsRoleWithCorrectProperties()
    {
        // Act
        var role = TestDataSeeder.CreateStudentRole();

        // Assert
        role.Should().NotBeNull();
        role.Id.Should().Be(2);
        role.Name.Should().Be("Student");
    }

    #endregion

    #region CreateAdminRole Tests

    [Fact]
    public void CreateAdminRole_ReturnsRoleWithCorrectProperties()
    {
        // Act
        var role = TestDataSeeder.CreateAdminRole();

        // Assert
        role.Should().NotBeNull();
        role.Id.Should().Be(3);
        role.Name.Should().Be("Admin");
    }

    #endregion

    #region CreateUserRole Tests

    [Fact]
    public void CreateUserRole_ReturnsRoleWithCorrectProperties()
    {
        // Act
        var role = TestDataSeeder.CreateUserRole();

        // Assert
        role.Should().NotBeNull();
        role.Id.Should().Be(4);
        role.Name.Should().Be("User");
    }

    #endregion

    #region CreateTestUser Tests

    [Fact]
    public void CreateTestUser_WithDefaults_ReturnsUserWithCorrectProperties()
    {
        // Act
        var user = TestDataSeeder.CreateTestUser();

        // Assert
        user.Should().NotBeNull();
        user.Login.Should().Be("testuser");
        user.PasswordHash.Should().NotBeNullOrEmpty();
        user.Role.Should().NotBeNull();
        user.InviteCode.Should().Be("TR-TEST01");
    }

    [Fact]
    public void CreateTestUser_WithCustomParameters_ReturnsUserWithCustomProperties()
    {
        // Arrange
        var customRole = new Role { Id = 5, Name = "CustomRole" };

        // Act
        var user = TestDataSeeder.CreateTestUser("customlogin", "custompassword", customRole);

        // Assert
        user.Login.Should().Be("customlogin");
        user.Role.Name.Should().Be("CustomRole");
    }

    [Fact]
    public void CreateTestUser_HashesPassword()
    {
        // Act
        var user = TestDataSeeder.CreateTestUser("user", "mypassword");

        // Assert
        user.PasswordHash.Should().NotBe("mypassword");
        BCrypt.Net.BCrypt.Verify("mypassword", user.PasswordHash).Should().BeTrue();
    }

    #endregion

    #region CreateTestDictionary Tests

    [Fact]
    public void CreateTestDictionary_WithDefaults_ReturnsDictionaryWithCorrectProperties()
    {
        // Act
        var dictionary = TestDataSeeder.CreateTestDictionary(1);

        // Assert
        dictionary.Should().NotBeNull();
        dictionary.UserId.Should().Be(1);
        dictionary.Name.Should().Be("Test Dictionary");
        dictionary.Description.Should().Be("Test description");
        dictionary.LanguageFrom.Should().Be("English");
        dictionary.LanguageTo.Should().Be("Russian");
        dictionary.Words.Should().NotBeNull();
    }

    [Fact]
    public void CreateTestDictionary_WithCustomName_ReturnsCorrectName()
    {
        // Act
        var dictionary = TestDataSeeder.CreateTestDictionary(1, "Custom Dictionary");

        // Assert
        dictionary.Name.Should().Be("Custom Dictionary");
    }

    #endregion

    #region CreateTestWord Tests

    [Fact]
    public void CreateTestWord_ReturnsWordWithCorrectProperties()
    {
        // Act
        var word = TestDataSeeder.CreateTestWord(1, 2);

        // Assert
        word.Should().NotBeNull();
        word.DictionaryId.Should().Be(1);
        word.UserId.Should().Be(2);
        word.OriginalWord.Should().Be("Hello");
        word.Translation.Should().Be("Привет");
        word.Example.Should().Be("Hello, world!");
    }

    #endregion

    #region CreateTestRule Tests

    [Fact]
    public void CreateTestRule_WithDefaults_ReturnsRuleWithCorrectProperties()
    {
        // Act
        var rule = TestDataSeeder.CreateTestRule(1);

        // Assert
        rule.Should().NotBeNull();
        rule.UserId.Should().Be(1);
        rule.Title.Should().Be("Test Rule");
        rule.Description.Should().Be("Test rule description");
        rule.MarkdownContent.Should().Contain("Test Rule");
    }

    [Fact]
    public void CreateTestRule_WithCustomTitle_ReturnsCorrectTitle()
    {
        // Act
        var rule = TestDataSeeder.CreateTestRule(1, "Custom Rule Title");

        // Assert
        rule.Title.Should().Be("Custom Rule Title");
    }

    #endregion
}
