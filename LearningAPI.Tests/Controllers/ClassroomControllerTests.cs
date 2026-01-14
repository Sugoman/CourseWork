using FluentAssertions;
using LearningAPI.Controllers;
using LearningAPI.Tests.Helpers;
using LearningTrainer.Context;
using LearningTrainerShared.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Xunit;

namespace LearningAPI.Tests.Controllers;

public class ClassroomControllerTests : IDisposable
{
    private readonly ApiDbContext _context;
    private readonly ClassroomController _controller;
    private readonly int _teacherId = 1;
    private readonly int _studentId = 2;

    public ClassroomControllerTests()
    {
        _context = TestDbContextFactory.CreateInMemoryContext();
        _controller = new ClassroomController(_context);
        SetupUserContext(_teacherId, "Teacher");
    }

    private void SetupUserContext(int userId, string role)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, role)
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    private async Task SetupTeacherAndStudent()
    {
        var teacherRole = TestDataSeeder.CreateTeacherRole();
        var studentRole = TestDataSeeder.CreateStudentRole();
        _context.Roles.AddRange(teacherRole, studentRole);
        
        var teacher = new User 
        { 
            Id = _teacherId, 
            Login = "teacher", 
            PasswordHash = "hash", 
            Role = teacherRole,
            InviteCode = "TR-ABC123"
        };
        
        var student = new User 
        { 
            Id = _studentId, 
            Login = "student", 
            PasswordHash = "hash", 
            Role = studentRole
        };
        
        _context.Users.AddRange(teacher, student);
        await _context.SaveChangesAsync();
    }

    #region GetMyInviteCode Tests

    [Fact]
    public async Task GetMyInviteCode_ReturnsExistingCode()
    {
        // Arrange
        await SetupTeacherAndStudent();

        // Act
        var result = await _controller.GetMyInviteCode();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value;
        response.Should().NotBeNull();
        
        var codeProperty = response!.GetType().GetProperty("Code");
        var code = codeProperty?.GetValue(response) as string;
        code.Should().Be("TR-ABC123");
    }

    [Fact]
    public async Task GetMyInviteCode_GeneratesNewCodeIfMissing()
    {
        // Arrange
        var role = TestDataSeeder.CreateTeacherRole();
        _context.Roles.Add(role);
        
        var user = new User 
        { 
            Id = _teacherId, 
            Login = "teacher", 
            PasswordHash = "hash", 
            Role = role,
            InviteCode = null // No invite code
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetMyInviteCode();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value;
        
        var codeProperty = response!.GetType().GetProperty("Code");
        var code = codeProperty?.GetValue(response) as string;
        code.Should().NotBeNullOrEmpty();
        code.Should().StartWith("TR-");
        
        // Verify code was saved
        var updatedUser = await _context.Users.FindAsync(_teacherId);
        updatedUser!.InviteCode.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region JoinClass Tests

    [Fact]
    public async Task JoinClass_WithValidCode_JoinsSuccessfully()
    {
        // Arrange
        await SetupTeacherAndStudent();
        SetupUserContext(_studentId, "Student");

        var request = new JoinClassRequest { Code = "TR-ABC123" };

        // Act
        var result = await _controller.JoinClass(request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        
        var student = await _context.Users.FindAsync(_studentId);
        student!.UserId.Should().Be(_teacherId);
    }

    [Fact]
    public async Task JoinClass_WithInvalidCode_ReturnsNotFound()
    {
        // Arrange
        await SetupTeacherAndStudent();
        SetupUserContext(_studentId, "Student");

        var request = new JoinClassRequest { Code = "INVALID-CODE" };

        // Act
        var result = await _controller.JoinClass(request);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task JoinClass_WithOwnCode_ReturnsBadRequest()
    {
        // Arrange
        await SetupTeacherAndStudent();
        // Teacher tries to join their own class
        SetupUserContext(_teacherId, "Teacher");

        var request = new JoinClassRequest { Code = "TR-ABC123" };

        // Act
        var result = await _controller.JoinClass(request);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    #endregion

    #region GetMyStudents Tests

    [Fact]
    public async Task GetMyStudents_ReturnsTeachersStudents()
    {
        // Arrange
        var teacherRole = TestDataSeeder.CreateTeacherRole();
        var studentRole = TestDataSeeder.CreateStudentRole();
        _context.Roles.AddRange(teacherRole, studentRole);
        
        var teacher = new User 
        { 
            Id = _teacherId, 
            Login = "teacher", 
            PasswordHash = "hash", 
            Role = teacherRole
        };
        
        var student1 = new User 
        { 
            Id = _studentId, 
            Login = "student1", 
            PasswordHash = "hash", 
            Role = studentRole,
            UserId = _teacherId // Belongs to teacher
        };
        
        var student2 = new User 
        { 
            Id = 3, 
            Login = "student2", 
            PasswordHash = "hash", 
            Role = studentRole,
            UserId = _teacherId // Belongs to teacher
        };
        
        var otherStudent = new User 
        { 
            Id = 4, 
            Login = "other", 
            PasswordHash = "hash", 
            Role = studentRole,
            UserId = 999 // Belongs to another teacher
        };
        
        _context.Users.AddRange(teacher, student1, student2, otherStudent);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetMyStudents();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var students = okResult.Value as IEnumerable<object>;
        students.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetMyStudents_ReturnsEmptyList_WhenNoStudents()
    {
        // Arrange
        var role = TestDataSeeder.CreateTeacherRole();
        _context.Roles.Add(role);
        
        var teacher = new User 
        { 
            Id = _teacherId, 
            Login = "teacher", 
            PasswordHash = "hash", 
            Role = role
        };
        _context.Users.Add(teacher);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetMyStudents();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var students = okResult.Value as IEnumerable<object>;
        students.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMyStudents_IncludesWordsLearnedCount()
    {
        // Arrange
        var teacherRole = TestDataSeeder.CreateTeacherRole();
        var studentRole = TestDataSeeder.CreateStudentRole();
        _context.Roles.AddRange(teacherRole, studentRole);
        
        var teacher = new User { Id = _teacherId, Login = "teacher", PasswordHash = "hash", Role = teacherRole };
        var student = new User { Id = _studentId, Login = "student", PasswordHash = "hash", Role = studentRole, UserId = _teacherId };
        _context.Users.AddRange(teacher, student);
        
        var dictionary = new Dictionary
        {
            Name = "Test",
            Description = "Test",
            LanguageFrom = "English",
            LanguageTo = "Russian",
            UserId = _teacherId,
            Words = new List<Word>()
        };
        _context.Dictionaries.Add(dictionary);
        await _context.SaveChangesAsync();
        
        var word = new Word
        {
            OriginalWord = "Hello",
            Translation = "Привет",
            Example = "",
            DictionaryId = dictionary.Id,
            UserId = _studentId
        };
        _context.Words.Add(word);
        await _context.SaveChangesAsync();
        
        // Student learned this word well (level > 3)
        var progress = new LearningProgress
        {
            UserId = _studentId,
            WordId = word.Id,
            KnowledgeLevel = 4,
            TotalAttempts = 5,
            CorrectAnswers = 4,
            LastPracticed = DateTime.UtcNow,
            NextReview = DateTime.UtcNow.AddDays(1)
        };
        _context.LearningProgresses.Add(progress);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetMyStudents();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().NotBeNull();
    }

    #endregion
}
