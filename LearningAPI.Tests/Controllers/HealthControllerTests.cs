using FluentAssertions;
using LearningAPI.Configuration;
using LearningAPI.Controllers;
using LearningAPI.Services;
using LearningAPI.Tests.Helpers;
using LearningTrainerShared.Context;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using static LearningAPI.Controllers.HealthController;

namespace LearningAPI.Tests.Controllers;

public class HealthControllerTests : IDisposable
{
    private readonly ApiDbContext _context;
    private readonly Mock<ILogger<HealthController>> _loggerMock;
    private readonly Mock<IHealthCheckService> _healthCheckServiceMock;
    private readonly Mock<IDistributedCache> _cacheMock;
    private readonly Mock<IWebHostEnvironment> _environmentMock;
    private readonly IOptions<HealthCheckConfiguration> _configOptions;
    private readonly HealthController _controller;

    public HealthControllerTests()
    {
        _context = TestDbContextFactory.CreateInMemoryContext();
        _loggerMock = new Mock<ILogger<HealthController>>();
        _healthCheckServiceMock = new Mock<IHealthCheckService>();
        _cacheMock = new Mock<IDistributedCache>();
        _environmentMock = new Mock<IWebHostEnvironment>();
        _environmentMock.Setup(e => e.EnvironmentName).Returns("Development");

        _configOptions = Options.Create(new HealthCheckConfiguration
        {
            CacheDurationSeconds = 30,
            Thresholds = new HealthCheckThresholds
            {
                MemoryWarningMB = 500,
                MemoryCriticalMB = 1000,
                DiskSpaceWarningGB = 5,
                DiskSpaceCriticalGB = 1
            }
        });

        // Setup default mocks
        _healthCheckServiceMock.Setup(s => s.CheckDatabaseAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HealthCheckResult { Name = "Database", Status = HealthStatus.Healthy, Message = "Connected" });

        _healthCheckServiceMock.Setup(s => s.CheckRedisAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HealthCheckResult { Name = "Redis", Status = HealthStatus.Healthy, Message = "Not configured" });

        _healthCheckServiceMock.Setup(s => s.CheckExternalDependenciesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ExternalDependencyResult>());

        _healthCheckServiceMock.Setup(s => s.GetBackgroundServicesStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BackgroundServiceStatus>());

        _healthCheckServiceMock.Setup(s => s.CheckMemory())
            .Returns(new MemoryHealthResult { Status = HealthStatus.Healthy, UsageMB = 100 });

        _healthCheckServiceMock.Setup(s => s.CheckDisk())
            .Returns(new DiskHealthResult { Status = HealthStatus.Healthy, AvailableGB = 50, TotalGB = 100 });

        _controller = new HealthController(
            _context, 
            _loggerMock.Object,
            _healthCheckServiceMock.Object,
            _cacheMock.Object,
            _configOptions,
            _environmentMock.Object);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public async Task Get_ReturnsHealthyStatus()
    {
        // Act
        var result = await _controller.Get(CancellationToken.None);

        // Assert
        var objectResult = result.Should().BeAssignableTo<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(200);

        var response = objectResult.Value as HealthCheckResponse;
        response.Should().NotBeNull();
        response!.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        response.Services.Should().NotBeNull();
    }

    [Fact]
    public async Task Get_ReturnsMemoryInfo()
    {
        // Act
        var result = await _controller.Get(CancellationToken.None);

        // Assert
        var objectResult = result.Should().BeAssignableTo<ObjectResult>().Subject;
        var response = objectResult.Value as HealthCheckResponse;

        response.Should().NotBeNull();
        response!.Services!.Memory.Should().NotBeNull();
        response.Services.Memory!.Status.Should().Be("Healthy");
    }

    [Fact]
    public async Task GetDetailed_ReturnsDetailedInfo()
    {
        // Act
        var result = await _controller.GetDetailed(CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value as DetailedHealthCheckResponse;

        response.Should().NotBeNull();
        response!.Status.Should().Be("Healthy");
        response.Version.Should().NotBeNullOrEmpty();
        response.Environment.Should().NotBeNullOrEmpty();
        response.System.Should().NotBeNull();
        response.System!.Processors.Should().BeGreaterThan(0);
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
        var result = await _controller.GetDetailed(CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value as DetailedHealthCheckResponse;

        response.Should().NotBeNull();
        response!.Database.Should().NotBeNull();
        response.Database!.Status.Should().Be("Healthy");
    }

    [Fact]
    public async Task GetMetrics_ReturnsPrometheusFormat()
    {
        // Act
        var result = await _controller.GetMetrics(CancellationToken.None);

        // Assert
        var contentResult = result.Should().BeOfType<ContentResult>().Subject;
        contentResult.ContentType.Should().Contain("text/plain");
        contentResult.Content.Should().Contain("app_health_status");
        contentResult.Content.Should().Contain("app_memory_usage_mb");
    }

    [Fact]
    public void GetLive_ReturnsAlive()
    {
        // Act
        var result = _controller.GetLive();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task GetReady_WhenDatabaseHealthy_ReturnsReady()
    {
        // Act
        var result = await _controller.GetReady(CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetReady_WhenDatabaseUnhealthy_Returns503()
    {
        // Arrange
        _healthCheckServiceMock.Setup(s => s.CheckDatabaseAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HealthCheckResult { Name = "Database", Status = HealthStatus.Unhealthy, Message = "Connection failed" });

        // Act
        var result = await _controller.GetReady(CancellationToken.None);

        // Assert
        var statusResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusResult.StatusCode.Should().Be(503);
    }
}
