using FluentAssertions;
using LearningAPI.Controllers;
using LearningAPI.Tests.Helpers;
using LearningTrainer.Context;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using static LearningAPI.Controllers.HealthController;

namespace LearningAPI.Tests.Controllers;

public class HealthControllerTests : IDisposable
{
    private readonly ApiDbContext _context;
    private readonly Mock<ILogger<HealthController>> _loggerMock;
    private readonly HealthController _controller;

    public HealthControllerTests()
    {
        _context = TestDbContextFactory.CreateInMemoryContext();
        _loggerMock = new Mock<ILogger<HealthController>>();
        _controller = new HealthController(_context, _loggerMock.Object);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public async Task Get_ReturnsHealthyStatus()
    {
        // Act
        var result = await _controller.Get();

        // Assert
        var objectResult = result.Should().BeAssignableTo<ObjectResult>().Subject;
        objectResult.StatusCode.Should().BeOneOf(200, 503);
        
        var response = objectResult.Value as HealthCheckResponse;
        response.Should().NotBeNull();
        response!.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        response.Services.Should().NotBeNull();
    }

    [Fact]
    public async Task Get_ReturnsMemoryInfo()
    {
        // Act
        var result = await _controller.Get();

        // Assert
        var objectResult = result.Should().BeAssignableTo<ObjectResult>().Subject;
        var response = objectResult.Value as HealthCheckResponse;
        
        response.Should().NotBeNull();
        response!.Services.Memory.Should().NotBeNullOrEmpty();
        response.Services.Memory.Should().Contain("MB");
    }

    [Fact]
    public async Task GetDetailed_ReturnsDetailedInfo()
    {
        // Act
        var result = await _controller.GetDetailed();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value as DetailedHealthCheckResponse;
        
        response.Should().NotBeNull();
        response!.Status.Should().Be("Healthy");
        response.Version.Should().NotBeNullOrEmpty();
        response.Environment.Should().NotBeNullOrEmpty();
        response.System.Should().NotBeNull();
        response.System.Processors.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetDetailed_ReturnsDatabaseMetrics()
    {
        // Arrange - add some test data
        var role = TestDataSeeder.CreateTeacherRole();
        _context.Roles.Add(role);
        
        var user = TestDataSeeder.CreateTestUser("testuser", "password", role);
        _context.Users.Add(user);
        
        var dictionary = TestDataSeeder.CreateTestDictionary(1);
        _context.Dictionaries.Add(dictionary);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetDetailed();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value as DetailedHealthCheckResponse;
        
        response.Should().NotBeNull();
        response!.Database.Should().NotBeNull();
        response.Database.Status.Should().Be("Healthy");
        response.Database.Metrics.Should().NotBeNull();
    }
}
