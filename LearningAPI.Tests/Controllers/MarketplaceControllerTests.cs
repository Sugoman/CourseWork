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

public class MarketplaceControllerTests : IDisposable
{
    private readonly ApiDbContext _context;
    private readonly Mock<ILogger<MarketplaceController>> _loggerMock;
    private readonly MarketplaceController _controller;
    private readonly int _testUserId = 1;

    public MarketplaceControllerTests()
    {
        _context = TestDbContextFactory.CreateInMemoryContext();
        _loggerMock = new Mock<ILogger<MarketplaceController>>();
        _controller = new MarketplaceController(_context, _loggerMock.Object);
        SetupUserContext(_testUserId, "Teacher");
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

    private async Task<User> CreateTestUser(int id, string login)
    {
        var role = TestDataSeeder.CreateTeacherRole();
        if (!await _context.Roles.AnyAsync(r => r.Id == role.Id))
        {
            _context.Roles.Add(role);
        }
        
        var user = new User { Id = id, Login = login, PasswordHash = "hash", Role = role };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    #region GetPublicDictionaries Tests

    [Fact]
    public async Task GetPublicDictionaries_ReturnsPublishedDictionaries()
    {
        // Arrange
        var user = await CreateTestUser(_testUserId, "testuser");
        
        var publishedDict = new Dictionary
        {
            Name = "Published Dict",
            Description = "Test",
            LanguageFrom = "English",
            LanguageTo = "Russian",
            UserId = _testUserId,
            IsPublished = true,
            Words = new List<Word>()
        };
        
        var unpublishedDict = new Dictionary
        {
            Name = "Unpublished Dict",
            Description = "Test",
            LanguageFrom = "English",
            LanguageTo = "Russian",
            UserId = _testUserId,
            IsPublished = false,
            Words = new List<Word>()
        };
        
        _context.Dictionaries.AddRange(publishedDict, unpublishedDict);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetPublicDictionaries(null, null, null, 1, 10);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value as PagedResultDto<DictionaryListItemDto>;
        response.Should().NotBeNull();
        response!.TotalCount.Should().Be(1);
        response.Items.First().Name.Should().Be("Published Dict");
    }

    [Fact]
    public async Task GetPublicDictionaries_FiltersBySearch()
    {
        // Arrange
        var user = await CreateTestUser(_testUserId, "testuser");
        
        var dict1 = new Dictionary
        {
            Name = "Animals Dictionary",
            Description = "Animals",
            LanguageFrom = "English",
            LanguageTo = "Russian",
            UserId = _testUserId,
            IsPublished = true,
            Words = new List<Word>()
        };
        
        var dict2 = new Dictionary
        {
            Name = "Colors Dictionary",
            Description = "Colors",
            LanguageFrom = "English",
            LanguageTo = "Russian",
            UserId = _testUserId,
            IsPublished = true,
            Words = new List<Word>()
        };
        
        _context.Dictionaries.AddRange(dict1, dict2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetPublicDictionaries("Animals", null, null, 1, 10);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value as PagedResultDto<DictionaryListItemDto>;
        response.Should().NotBeNull();
        response!.TotalCount.Should().Be(1);
        response.Items.First().Name.Should().Contain("Animals");
    }

    [Fact]
    public async Task GetPublicDictionaries_FiltersByLanguage()
    {
        // Arrange
        var user = await CreateTestUser(_testUserId, "testuser");
        
        var dict1 = new Dictionary
        {
            Name = "English to Russian",
            Description = "Test",
            LanguageFrom = "English",
            LanguageTo = "Russian",
            UserId = _testUserId,
            IsPublished = true,
            Words = new List<Word>()
        };
        
        var dict2 = new Dictionary
        {
            Name = "German to English",
            Description = "Test",
            LanguageFrom = "German",
            LanguageTo = "English",
            UserId = _testUserId,
            IsPublished = true,
            Words = new List<Word>()
        };
        
        _context.Dictionaries.AddRange(dict1, dict2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetPublicDictionaries(null, "English", "Russian", 1, 10);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value as PagedResultDto<DictionaryListItemDto>;
        response.Should().NotBeNull();
        response!.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task GetPublicDictionaries_SupportsPagination()
    {
        // Arrange
        var user = await CreateTestUser(_testUserId, "testuser");
        
        for (int i = 1; i <= 15; i++)
        {
            _context.Dictionaries.Add(new Dictionary
            {
                Name = $"Dictionary {i}",
                Description = "Test",
                LanguageFrom = "English",
                LanguageTo = "Russian",
                UserId = _testUserId,
                IsPublished = true,
                Rating = i, // For ordering
                Words = new List<Word>()
            });
        }
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetPublicDictionaries(null, null, null, 2, 5);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value as PagedResultDto<DictionaryListItemDto>;
        response.Should().NotBeNull();
        response!.TotalCount.Should().Be(15);
        response.TotalPages.Should().Be(3);
        response.CurrentPage.Should().Be(2);
        response.Items.Should().HaveCount(5);
    }

    #endregion

    #region GetDictionaryDetails Tests

    [Fact]
    public async Task GetDictionaryDetails_WithValidId_ReturnsDictionaryDetails()
    {
        // Arrange
        var user = await CreateTestUser(_testUserId, "testuser");
        
        var dictionary = new Dictionary
        {
            Name = "Test Dictionary",
            Description = "Test description",
            LanguageFrom = "English",
            LanguageTo = "Russian",
            UserId = _testUserId,
            IsPublished = true,
            Rating = 4.5,
            RatingCount = 10,
            DownloadCount = 100,
            Words = new List<Word>()
        };
        _context.Dictionaries.Add(dictionary);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetDictionaryDetails(dictionary.Id);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var details = okResult.Value as DictionaryDetailsDto;
        details.Should().NotBeNull();
        details!.Name.Should().Be("Test Dictionary");
        details.Rating.Should().Be(4.5);
    }

    [Fact]
    public async Task GetDictionaryDetails_WithNonExistentId_ReturnsNotFound()
    {
        // Act
        var result = await _controller.GetDictionaryDetails(999);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetDictionaryDetails_WithUnpublishedDictionary_ReturnsNotFound()
    {
        // Arrange
        var user = await CreateTestUser(_testUserId, "testuser");
        
        var dictionary = new Dictionary
        {
            Id = 1,
            Name = "Unpublished",
            Description = "Test",
            LanguageFrom = "English",
            LanguageTo = "Russian",
            UserId = _testUserId,
            IsPublished = false,
            Words = new List<Word>()
        };
        _context.Dictionaries.Add(dictionary);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetDictionaryDetails(1);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    #endregion

    #region DownloadDictionary Tests

    [Fact]
    public async Task DownloadDictionary_WithValidId_CreatesCopyAndReturnsOk()
    {
        // Arrange
        var user = await CreateTestUser(_testUserId, "testuser");
        
        var originalDict = new Dictionary
        {
            Name = "Original Dictionary",
            Description = "Original description",
            LanguageFrom = "English",
            LanguageTo = "Russian",
            UserId = 2, // Different user
            IsPublished = true,
            DownloadCount = 5,
            Words = new List<Word>()
        };
        _context.Dictionaries.Add(originalDict);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.DownloadDictionary(originalDict.Id);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        
        // Check that a copy was created
        var userDictionaries = await _context.Dictionaries
            .Where(d => d.UserId == _testUserId)
            .ToListAsync();
        userDictionaries.Should().HaveCount(1);
        userDictionaries.First().SourceDictionaryId.Should().Be(originalDict.Id);
        
        // Check download count increased
        var original = await _context.Dictionaries.FindAsync(originalDict.Id);
        original!.DownloadCount.Should().Be(6);
        
        // Check download history
        var downloads = await _context.Downloads.ToListAsync();
        downloads.Should().HaveCount(1);
    }

    [Fact]
    public async Task DownloadDictionary_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        await CreateTestUser(_testUserId, "testuser");

        // Act
        var result = await _controller.DownloadDictionary(999);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    #endregion

    #region GetPublicRules Tests

    [Fact]
    public async Task GetPublicRules_ReturnsPublishedRules()
    {
        // Arrange
        var user = await CreateTestUser(_testUserId, "testuser");
        
        var publishedRule = new Rule
        {
            Title = "Published Rule",
            MarkdownContent = "Content",
            Description = "Test",
            Category = "Grammar",
            UserId = _testUserId,
            IsPublished = true
        };
        
        var unpublishedRule = new Rule
        {
            Title = "Unpublished Rule",
            MarkdownContent = "Content",
            Description = "Test",
            Category = "Grammar",
            UserId = _testUserId,
            IsPublished = false
        };
        
        _context.Rules.AddRange(publishedRule, unpublishedRule);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetPublicRules(null, null, 0, 1, 10);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value as PagedResultDto<RuleListItemDto>;
        response.Should().NotBeNull();
        response!.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task GetPublicRules_FiltersByCategory()
    {
        // Arrange
        var user = await CreateTestUser(_testUserId, "testuser");
        
        var grammarRule = new Rule
        {
            Title = "Grammar Rule",
            MarkdownContent = "Content",
            Description = "Test description",
            Category = "Grammar",
            UserId = _testUserId,
            IsPublished = true
        };
        
        var vocabularyRule = new Rule
        {
            Title = "Vocabulary Rule",
            MarkdownContent = "Content",
            Description = "Test description",
            Category = "Vocabulary",
            UserId = _testUserId,
            IsPublished = true
        };
        
        _context.Rules.AddRange(grammarRule, vocabularyRule);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetPublicRules(null, "Grammar", 0, 1, 10);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value as PagedResultDto<RuleListItemDto>;
        response.Should().NotBeNull();
        response!.TotalCount.Should().Be(1);
        response.Items.First().Category.Should().Be("Grammar");
    }

    [Fact]
    public async Task GetPublicRules_FiltersByDifficulty()
    {
        // Arrange
        var user = await CreateTestUser(_testUserId, "testuser");
        
        var easyRule = new Rule
        {
            Title = "Easy Rule",
            MarkdownContent = "Content",
            Description = "Test description",
            Category = "Grammar",
            DifficultyLevel = 1,
            UserId = _testUserId,
            IsPublished = true
        };
        
        var hardRule = new Rule
        {
            Title = "Hard Rule",
            MarkdownContent = "Content",
            Description = "Test description",
            Category = "Grammar",
            DifficultyLevel = 3,
            UserId = _testUserId,
            IsPublished = true
        };
        
        _context.Rules.AddRange(easyRule, hardRule);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetPublicRules(null, null, 3, 1, 10);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value as PagedResultDto<RuleListItemDto>;
        response.Should().NotBeNull();
        response!.TotalCount.Should().Be(1);
        response.Items.First().DifficultyLevel.Should().Be(3);
    }

    #endregion

    #region DownloadRule Tests

    [Fact]
    public async Task DownloadRule_WithValidId_CreatesCopyAndReturnsOk()
    {
        // Arrange
        var user = await CreateTestUser(_testUserId, "testuser");
        
        var originalRule = new Rule
        {
            Id = 1,
            Title = "Original Rule",
            MarkdownContent = "Content",
            Description = "Description",
            Category = "Grammar",
            DifficultyLevel = 2,
            UserId = 2, // Different user
            IsPublished = true,
            DownloadCount = 10
        };
        _context.Rules.Add(originalRule);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.DownloadRule(1);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        
        // Check that a copy was created
        var userRules = await _context.Rules
            .Where(r => r.UserId == _testUserId)
            .ToListAsync();
        userRules.Should().HaveCount(1);
        userRules.First().SourceRuleId.Should().Be(1);
        
        // Check download count increased
        var original = await _context.Rules.FindAsync(1);
        original!.DownloadCount.Should().Be(11);
    }

    #endregion

    #region Comments Tests

    [Fact]
    public async Task AddDictionaryComment_WithValidData_AddsComment()
    {
        // Arrange
        var user = await CreateTestUser(_testUserId, "testuser");
        
        var dictionary = new Dictionary
        {
            Id = 1,
            Name = "Test",
            Description = "Test",
            LanguageFrom = "English",
            LanguageTo = "Russian",
            UserId = _testUserId,
            IsPublished = true,
            Words = new List<Word>()
        };
        _context.Dictionaries.Add(dictionary);
        await _context.SaveChangesAsync();

        var request = new AddCommentRequest
        {
            Rating = 5,
            Text = "Great dictionary!"
        };

        // Act
        var result = await _controller.AddDictionaryComment(1, request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        
        var comments = await _context.Comments.ToListAsync();
        comments.Should().HaveCount(1);
        comments.First().Rating.Should().Be(5);
        comments.First().Text.Should().Be("Great dictionary!");
    }

    [Fact]
    public async Task GetDictionaryComments_ReturnsCommentsForDictionary()
    {
        // Arrange
        var user = await CreateTestUser(_testUserId, "testuser");
        
        var comment = new Comment
        {
            UserId = _testUserId,
            ContentType = "Dictionary",
            ContentId = 1,
            Rating = 4,
            Text = "Good!",
            CreatedAt = DateTime.UtcNow
        };
        _context.Comments.Add(comment);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetDictionaryComments(1);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var comments = okResult.Value as List<CommentItemDto>;
        comments.Should().HaveCount(1);
    }

    #endregion

    #region My Content Tests

    [Fact]
    public async Task GetMyDictionaries_ReturnsUserDictionaries()
    {
        // Arrange
        var user = await CreateTestUser(_testUserId, "testuser");
        
        var dict1 = new Dictionary
        {
            Name = "My Dict 1",
            Description = "Test",
            LanguageFrom = "English",
            LanguageTo = "Russian",
            UserId = _testUserId,
            Words = new List<Word>()
        };
        
        var dict2 = new Dictionary
        {
            Name = "Other Dict",
            Description = "Test",
            LanguageFrom = "English",
            LanguageTo = "Russian",
            UserId = 999,
            Words = new List<Word>()
        };
        
        _context.Dictionaries.AddRange(dict1, dict2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetMyDictionaries();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var dictionaries = okResult.Value as List<MyDictionaryItemDto>;
        dictionaries.Should().HaveCount(1);
        dictionaries.First().Name.Should().Be("My Dict 1");
    }

    [Fact]
    public async Task PublishDictionary_WithValidId_PublishesDictionary()
    {
        // Arrange
        var user = await CreateTestUser(_testUserId, "testuser");
        
        var dictionary = new Dictionary
        {
            Id = 1,
            Name = "My Dict",
            Description = "Test",
            LanguageFrom = "English",
            LanguageTo = "Russian",
            UserId = _testUserId,
            IsPublished = false,
            Words = new List<Word>()
        };
        _context.Dictionaries.Add(dictionary);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.PublishDictionary(1);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        
        var updatedDict = await _context.Dictionaries.FindAsync(1);
        updatedDict!.IsPublished.Should().BeTrue();
    }

    [Fact]
    public async Task PublishDictionary_WithOtherUsersDictionary_ReturnsNotFound()
    {
        // Arrange
        var user = await CreateTestUser(_testUserId, "testuser");
        
        var dictionary = new Dictionary
        {
            Id = 1,
            Name = "Other Dict",
            Description = "Test",
            LanguageFrom = "English",
            LanguageTo = "Russian",
            UserId = 999, // Different user
            IsPublished = false,
            Words = new List<Word>()
        };
        _context.Dictionaries.Add(dictionary);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.PublishDictionary(1);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    #endregion
}
