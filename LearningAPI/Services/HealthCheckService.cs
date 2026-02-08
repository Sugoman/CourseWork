using LearningAPI.Configuration;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Diagnostics;
using System.Text.Json;

namespace LearningAPI.Services;

/// <summary>
/// Сервис для проверки здоровья системы и зависимостей
/// </summary>
public interface IHealthCheckService
{
    Task<HealthCheckResult> CheckDatabaseAsync(CancellationToken cancellationToken = default);
    Task<HealthCheckResult> CheckRedisAsync(CancellationToken cancellationToken = default);
    Task<List<ExternalDependencyResult>> CheckExternalDependenciesAsync(CancellationToken cancellationToken = default);
    Task<List<BackgroundServiceStatus>> GetBackgroundServicesStatusAsync(CancellationToken cancellationToken = default);
    MemoryHealthResult CheckMemory();
    DiskHealthResult CheckDisk();
}

public class HealthCheckService : IHealthCheckService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<HealthCheckService> _logger;
    private readonly HealthCheckConfiguration _config;

    public HealthCheckService(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        ILogger<HealthCheckService> logger,
        IOptions<HealthCheckConfiguration> config)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _config = config.Value;
    }

    public async Task<HealthCheckResult> CheckDatabaseAsync(CancellationToken cancellationToken = default)
    {
        var result = new HealthCheckResult { Name = "Database" };
        var sw = Stopwatch.StartNew();

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<LearningTrainer.Context.ApiDbContext>();
            
            var canConnect = await context.Database.CanConnectAsync(cancellationToken);
            sw.Stop();

            result.ResponseTimeMs = sw.ElapsedMilliseconds;
            result.Status = canConnect ? HealthStatus.Healthy : HealthStatus.Unhealthy;
            result.Message = canConnect ? "Connected" : "Connection failed";

            if (canConnect)
            {
                result.Metrics = new Dictionary<string, object>
                {
                    ["responseTimeMs"] = sw.ElapsedMilliseconds
                };
            }
        }
        catch (Exception ex)
        {
            sw.Stop();
            result.Status = HealthStatus.Unhealthy;
            result.Message = ex.Message;
            result.ResponseTimeMs = sw.ElapsedMilliseconds;
            _logger.LogError(ex, "Database health check failed");
        }

        return result;
    }

    public async Task<HealthCheckResult> CheckRedisAsync(CancellationToken cancellationToken = default)
    {
        var result = new HealthCheckResult { Name = "Redis" };
        var sw = Stopwatch.StartNew();

        var redisConnection = _configuration.GetConnectionString("Redis");
        
        if (string.IsNullOrEmpty(redisConnection))
        {
            result.Status = HealthStatus.Healthy;
            result.Message = "Redis not configured (using in-memory cache)";
            return result;
        }

        try
        {
            var redis = await ConnectionMultiplexer.ConnectAsync(redisConnection);
            var db = redis.GetDatabase();
            
            // Простая операция ping
            var pingResult = await db.PingAsync();
            sw.Stop();

            result.Status = HealthStatus.Healthy;
            result.Message = "Connected";
            result.ResponseTimeMs = sw.ElapsedMilliseconds;
            result.Metrics = new Dictionary<string, object>
            {
                ["pingMs"] = pingResult.TotalMilliseconds,
                ["responseTimeMs"] = sw.ElapsedMilliseconds,
                ["isConnected"] = redis.IsConnected
            };

            await redis.CloseAsync();
        }
        catch (Exception ex)
        {
            sw.Stop();
            result.Status = HealthStatus.Unhealthy;
            result.Message = $"Redis connection failed: {ex.Message}";
            result.ResponseTimeMs = sw.ElapsedMilliseconds;
            _logger.LogError(ex, "Redis health check failed");
        }

        return result;
    }

    public async Task<List<ExternalDependencyResult>> CheckExternalDependenciesAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<ExternalDependencyResult>();
        var enabledDependencies = _config.ExternalDependencies.Where(d => d.Enabled).ToList();

        foreach (var dependency in enabledDependencies)
        {
            var result = new ExternalDependencyResult { Name = dependency.Name };
            var sw = Stopwatch.StartNew();

            try
            {
                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(dependency.TimeoutSeconds);

                var response = await client.GetAsync(dependency.Url, cancellationToken);
                sw.Stop();

                result.Status = response.IsSuccessStatusCode ? HealthStatus.Healthy : HealthStatus.Unhealthy;
                result.StatusCode = (int)response.StatusCode;
                result.ResponseTimeMs = sw.ElapsedMilliseconds;
                result.Message = response.IsSuccessStatusCode ? "OK" : $"HTTP {response.StatusCode}";
            }
            catch (TaskCanceledException)
            {
                sw.Stop();
                result.Status = HealthStatus.Unhealthy;
                result.Message = "Timeout";
                result.ResponseTimeMs = sw.ElapsedMilliseconds;
            }
            catch (Exception ex)
            {
                sw.Stop();
                result.Status = HealthStatus.Unhealthy;
                result.Message = ex.Message;
                result.ResponseTimeMs = sw.ElapsedMilliseconds;
                _logger.LogWarning(ex, "External dependency check failed for {Name}", dependency.Name);
            }

            results.Add(result);
        }

        return results;
    }

    public async Task<List<BackgroundServiceStatus>> GetBackgroundServicesStatusAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<BackgroundServiceStatus>();

        // Получаем все зарегистрированные IHostedService
        using var scope = _serviceProvider.CreateScope();
        var hostedServices = scope.ServiceProvider.GetServices<IHostedService>();

        foreach (var service in hostedServices)
        {
            var serviceName = service.GetType().Name;
            var status = new BackgroundServiceStatus
            {
                Name = serviceName,
                Status = HealthStatus.Healthy,
                Message = "Running"
            };

            // Проверяем, является ли сервис BackgroundService
            if (service is BackgroundService bgService)
            {
                var executingTask = GetExecutingTask(bgService);
                if (executingTask != null)
                {
                    status.IsRunning = !executingTask.IsCompleted;
                    if (executingTask.IsFaulted)
                    {
                        status.Status = HealthStatus.Unhealthy;
                        status.Message = executingTask.Exception?.InnerException?.Message ?? "Faulted";
                    }
                    else if (executingTask.IsCompleted)
                    {
                        status.Status = HealthStatus.Degraded;
                        status.Message = "Completed";
                    }
                }
            }

            results.Add(status);
        }

        return await Task.FromResult(results);
    }

    private static Task? GetExecutingTask(BackgroundService service)
    {
        try
        {
            var field = typeof(BackgroundService).GetField("_executeTask", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return field?.GetValue(service) as Task;
        }
        catch
        {
            return null;
        }
    }

    public MemoryHealthResult CheckMemory()
    {
        var memoryUsageMB = GC.GetTotalMemory(false) / (1024 * 1024);
        var gen0Collections = GC.CollectionCount(0);
        var gen1Collections = GC.CollectionCount(1);
        var gen2Collections = GC.CollectionCount(2);

        var status = HealthStatus.Healthy;
        if (memoryUsageMB >= _config.Thresholds.MemoryCriticalMB)
            status = HealthStatus.Unhealthy;
        else if (memoryUsageMB >= _config.Thresholds.MemoryWarningMB)
            status = HealthStatus.Degraded;

        return new MemoryHealthResult
        {
            Status = status,
            UsageMB = memoryUsageMB,
            Gen0Collections = gen0Collections,
            Gen1Collections = gen1Collections,
            Gen2Collections = gen2Collections,
            ThresholdWarningMB = _config.Thresholds.MemoryWarningMB,
            ThresholdCriticalMB = _config.Thresholds.MemoryCriticalMB
        };
    }

    public DiskHealthResult CheckDisk()
    {
        try
        {
            var driveInfo = new DriveInfo(Path.GetPathRoot(Environment.CurrentDirectory) ?? "C:\\");
            var availableGB = driveInfo.AvailableFreeSpace / (1024 * 1024 * 1024);
            var totalGB = driveInfo.TotalSize / (1024 * 1024 * 1024);

            var status = HealthStatus.Healthy;
            if (availableGB <= _config.Thresholds.DiskSpaceCriticalGB)
                status = HealthStatus.Unhealthy;
            else if (availableGB <= _config.Thresholds.DiskSpaceWarningGB)
                status = HealthStatus.Degraded;

            return new DiskHealthResult
            {
                Status = status,
                AvailableGB = availableGB,
                TotalGB = totalGB,
                UsagePercent = (double)(totalGB - availableGB) / totalGB * 100,
                ThresholdWarningGB = _config.Thresholds.DiskSpaceWarningGB,
                ThresholdCriticalGB = _config.Thresholds.DiskSpaceCriticalGB
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check disk space");
            return new DiskHealthResult
            {
                Status = HealthStatus.Unknown,
                Message = ex.Message
            };
        }
    }
}

#region Result Models

public enum HealthStatus
{
    Healthy,
    Degraded,
    Unhealthy,
    Unknown
}

public class HealthCheckResult
{
    public string Name { get; set; } = string.Empty;
    public HealthStatus Status { get; set; }
    public string Message { get; set; } = string.Empty;
    public long ResponseTimeMs { get; set; }
    public Dictionary<string, object>? Metrics { get; set; }
}

public class ExternalDependencyResult
{
    public string Name { get; set; } = string.Empty;
    public HealthStatus Status { get; set; }
    public string Message { get; set; } = string.Empty;
    public int? StatusCode { get; set; }
    public long ResponseTimeMs { get; set; }
}

public class BackgroundServiceStatus
{
    public string Name { get; set; } = string.Empty;
    public HealthStatus Status { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool IsRunning { get; set; } = true;
}

public class MemoryHealthResult
{
    public HealthStatus Status { get; set; }
    public long UsageMB { get; set; }
    public int Gen0Collections { get; set; }
    public int Gen1Collections { get; set; }
    public int Gen2Collections { get; set; }
    public long ThresholdWarningMB { get; set; }
    public long ThresholdCriticalMB { get; set; }
}

public class DiskHealthResult
{
    public HealthStatus Status { get; set; }
    public long AvailableGB { get; set; }
    public long TotalGB { get; set; }
    public double UsagePercent { get; set; }
    public long ThresholdWarningGB { get; set; }
    public long ThresholdCriticalGB { get; set; }
    public string? Message { get; set; }
}

#endregion
