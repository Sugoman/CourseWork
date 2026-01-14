using FluentAssertions;
using LearningAPI.Controllers;
using LearningAPI.Tests.Helpers;
using LearningTrainer.Context;
using LearningTrainerShared.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;
using Xunit;

namespace LearningAPI.Tests.Controllers;

public class SharingControllerTests : IDisposable
{
    private readonly ApiDbContext _context;
    private readonly Mock<ILogger<SharingController>> _loggerMock;
    private readonly SharingController _controller;
    private readonly int _teacherId = 1;
    private readonly int _studentId = 2;

    public SharingControllerTests()
    {
        _context = TestDbContextFactory.CreateInMemoryContext();
        _loggerMock = new Mock<ILogger<SharingController>>();
        _controller = new SharingController(_context, _loggerMock.Object);
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
            InviteCode = "TR-TEST01"
        };
        
        var student = new User 
        { 
            Id = _studentId, 
            Login = "student", 
            PasswordHash = "hash", 
            Role = studentRole,
            UserId = _teacherId // Student belongs to teacher
        };
        
        _context.Users.AddRange(teacher, student);
        await _context.SaveChangesAsync();
    }

    #region Dictionary Sharing Status Tests

    [Fact]
    public async Task GetDictionarySharingStatus_WithValidDictionary_ReturnsSharedStudentIds()
    {
        // Arrange
        await SetupTeacherAndStudent();
        
        var dictionary = new Dictionary
        {
            Id = 1,
            Name = "Test Dict",
            Description = "Test",
            LanguageFrom = "English",
            LanguageTo = "Russian",
            UserId = _teacherId,
            Words = new List<Word>()
        };
        _context.Dictionaries.Add(dictionary);
        
        var sharing = new DictionarySharing
        {
            DictionaryId = 1,
            StudentId = _studentId,
            SharedAt = DateTime.UtcNow
        };
        _context.DictionarySharings.Add(sharing);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetDictionarySharingStatus(1);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var sharedIds = okResult.Value as List<int>;
        sharedIds.Should().Contain(_studentId);
    }

    [Fact]
    public async Task GetDictionarySharingStatus_WithOtherUsersDictionary_ReturnsNotFound()
    {
        // Arrange
        await SetupTeacherAndStudent();
        
        var dictionary = new Dictionary
        {
            Id = 1,
            Name = "Other Dict",
            Description = "Test",
            LanguageFrom = "English",
            LanguageTo = "Russian",
            UserId = 999, // Different user
            Words = new List<Word>()
        };
        _context.Dictionaries.Add(dictionary);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetDictionarySharingStatus(1);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetDictionarySharingStatus_WithNonExistentDictionary_ReturnsNotFound()
    {
        // Arrange
        await SetupTeacherAndStudent();

        // Act
        var result = await _controller.GetDictionarySharingStatus(999);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    #endregion

    #region Toggle Dictionary Sharing Tests

    [Fact]
    public async Task ToggleDictionarySharing_SharesNewDictionary()
    {
        // Arrange
        await SetupTeacherAndStudent();
        
        var dictionary = new Dictionary
        {
            Id = 1,
            Name = "Test Dict",
            Description = "Test",
            LanguageFrom = "English",
            LanguageTo = "Russian",
            UserId = _teacherId,
            Words = new List<Word>()
        };
        _context.Dictionaries.Add(dictionary);
        await _context.SaveChangesAsync();

        var request = new ToggleSharingRequest
        {
            ContentId = 1,
            StudentId = _studentId
        };

        // Act
        var result = await _controller.ToggleDictionarySharing(request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        
        var sharing = await _context.DictionarySharings
            .FirstOrDefaultAsync(ds => ds.DictionaryId == 1 && ds.StudentId == _studentId);
        sharing.Should().NotBeNull();
    }

    [Fact]
    public async Task ToggleDictionarySharing_UnshareExistingDictionary()
    {
        // Arrange
        await SetupTeacherAndStudent();
        
        var dictionary = new Dictionary
        {
            Id = 1,
            Name = "Test Dict",
            Description = "Test",
            LanguageFrom = "English",
            LanguageTo = "Russian",
            UserId = _teacherId,
            Words = new List<Word>()
        };
        _context.Dictionaries.Add(dictionary);
        
        var existingSharing = new DictionarySharing
        {
            DictionaryId = 1,
            StudentId = _studentId,
            SharedAt = DateTime.UtcNow
        };
        _context.DictionarySharings.Add(existingSharing);
        await _context.SaveChangesAsync();

        var request = new ToggleSharingRequest
        {
            ContentId = 1,
            StudentId = _studentId
        };

        // Act
        var result = await _controller.ToggleDictionarySharing(request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        
        var sharing = await _context.DictionarySharings
            .FirstOrDefaultAsync(ds => ds.DictionaryId == 1 && ds.StudentId == _studentId);
        sharing.Should().BeNull();
    }

    [Fact]
    public async Task ToggleDictionarySharing_WithNonExistentDictionary_ReturnsNotFound()
    {
        // Arrange
        await SetupTeacherAndStudent();

        var request = new ToggleSharingRequest
        {
            ContentId = 999,
            StudentId = _studentId
        };

        // Act
        var result = await _controller.ToggleDictionarySharing(request);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task ToggleDictionarySharing_WithNonExistentStudent_ReturnsNotFound()
    {
        // Arrange
        await SetupTeacherAndStudent();
        
        var dictionary = new Dictionary
        {
            Id = 1,
            Name = "Test Dict",
            Description = "Test",
            LanguageFrom = "English",
            LanguageTo = "Russian",
            UserId = _teacherId,
            Words = new List<Word>()
        };
        _context.Dictionaries.Add(dictionary);
        await _context.SaveChangesAsync();

        var request = new ToggleSharingRequest
        {
            ContentId = 1,
            StudentId = 999 // Non-existent student
        };

        // Act
        var result = await _controller.ToggleDictionarySharing(request);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    #endregion

    #region Rule Sharing Status Tests

    [Fact]
    public async Task GetRuleSharingStatus_WithValidRule_ReturnsSharedStudentIds()
    {
        // Arrange
        await SetupTeacherAndStudent();
        
        var rule = new Rule
        {
            Id = 1,
            Title = "Test Rule",
            MarkdownContent = "Content",
            Description = "Test",
            Category = "Grammar",
            UserId = _teacherId
        };
        _context.Rules.Add(rule);
        
        var sharing = new RuleSharing
        {
            RuleId = 1,
            StudentId = _studentId,
            SharedAt = DateTime.UtcNow
        };
        _context.RuleSharings.Add(sharing);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetRuleSharingStatus(1);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var sharedIds = okResult.Value as List<int>;
        sharedIds.Should().Contain(_studentId);
    }

    [Fact]
    public async Task GetRuleSharingStatus_WithOtherUsersRule_ReturnsNotFound()
    {
        // Arrange
        await SetupTeacherAndStudent();
        
        var rule = new Rule
        {
            Id = 1,
            Title = "Other Rule",
            MarkdownContent = "Content",
            Description = "Test",
            Category = "Grammar",
            UserId = 999
        };
        _context.Rules.Add(rule);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetRuleSharingStatus(1);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    #endregion

    #region Toggle Rule Sharing Tests

    [Fact]
    public async Task ToggleRuleSharing_SharesNewRule()
    {
        // Arrange
        await SetupTeacherAndStudent();
        
        var rule = new Rule
        {
            Id = 1,
            Title = "Test Rule",
            MarkdownContent = "Content",
            Description = "Test",
            Category = "Grammar",
            UserId = _teacherId
        };
        _context.Rules.Add(rule);
        await _context.SaveChangesAsync();

        var request = new ToggleSharingRequest
        {
            ContentId = 1,
            StudentId = _studentId
        };

        // Act
        var result = await _controller.ToggleRuleSharing(request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        
        var sharing = await _context.RuleSharings
            .FirstOrDefaultAsync(rs => rs.RuleId == 1 && rs.StudentId == _studentId);
        sharing.Should().NotBeNull();
    }

    [Fact]
    public async Task ToggleRuleSharing_UnshareExistingRule()
    {
        // Arrange
        await SetupTeacherAndStudent();
        
        var rule = new Rule
        {
            Id = 1,
            Title = "Test Rule",
            MarkdownContent = "Content",
            Description = "Test",
            Category = "Grammar",
            UserId = _teacherId
        };
        _context.Rules.Add(rule);
        
        var existingSharing = new RuleSharing
        {
            RuleId = 1,
            StudentId = _studentId,
            SharedAt = DateTime.UtcNow
        };
        _context.RuleSharings.Add(existingSharing);
        await _context.SaveChangesAsync();

        var request = new ToggleSharingRequest
        {
            ContentId = 1,
            StudentId = _studentId
        };

        // Act
        var result = await _controller.ToggleRuleSharing(request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        
        var sharing = await _context.RuleSharings
            .FirstOrDefaultAsync(rs => rs.RuleId == 1 && rs.StudentId == _studentId);
        sharing.Should().BeNull();
    }

    #endregion
}
