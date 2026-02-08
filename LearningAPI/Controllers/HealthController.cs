using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using LearningTrainer.Context;
using LearningAPI.Configuration;
using LearningAPI.Services;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LearningAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HealthController : ControllerBase
    {
        private readonly ApiDbContext _context;
        private readonly ILogger<HealthController> _logger;
        private readonly IHealthCheckService _healthCheckService;
        private readonly IDistributedCache _cache;
        private readonly HealthCheckConfiguration _config;
        private readonly IWebHostEnvironment _environment;

        private const string HealthCacheKey = "health_check_result";
        private const string DetailedHealthCacheKey = "health_check_detailed_result";

        public HealthController(
            ApiDbContext context,
            ILogger<HealthController> logger,
            IHealthCheckService healthCheckService,
            IDistributedCache cache,
            IOptions<HealthCheckConfiguration> config,
            IWebHostEnvironment environment)
        {
            _context = context;
            _logger = logger;
            _healthCheckService = healthCheckService;
            _cache = cache;
            _config = config.Value;
            _environment = environment;
        }

        /// <summary>
        /// Проверка состояния API
        /// </summary>
        /// <returns>Статус здоровья приложения</returns>
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Get(CancellationToken cancellationToken)
        {
            var sw = Stopwatch.StartNew();

            try
            {
                // Проверяем кэш в production
                if (_environment.IsProduction())
                {
                    var cached = await GetCachedResultAsync<HealthCheckResponse>(HealthCacheKey, cancellationToken);
                    if (cached != null)
                    {
                        cached.FromCache = true;
                        return StatusCode(GetStatusCode(cached.Status), cached);
                    }
                }

                var response = new HealthCheckResponse
                {
                    Status = "Healthy",
                    Timestamp = DateTime.UtcNow,
                    Services = new ServiceStatus(),
                    FromCache = false
                };

                // Проверка БД
                var dbResult = await _healthCheckService.CheckDatabaseAsync(cancellationToken);
                response.Services.Database = new ServiceHealthInfo
                {
                    Status = dbResult.Status.ToString(),
                    ResponseTimeMs = dbResult.ResponseTimeMs,
                    Message = dbResult.Message
                };
                UpdateOverallStatus(response, dbResult.Status);

                // Проверка Redis
                var redisResult = await _healthCheckService.CheckRedisAsync(cancellationToken);
                response.Services.Redis = new ServiceHealthInfo
                {
                    Status = redisResult.Status.ToString(),
                    ResponseTimeMs = redisResult.ResponseTimeMs,
                    Message = redisResult.Message
                };
                UpdateOverallStatus(response, redisResult.Status);

                // Проверка памяти
                var memoryResult = _healthCheckService.CheckMemory();
                response.Services.Memory = new MemoryHealthInfo
                {
                    Status = memoryResult.Status.ToString(),
                    UsageMB = memoryResult.UsageMB,
                    ThresholdWarningMB = memoryResult.ThresholdWarningMB,
                    ThresholdCriticalMB = memoryResult.ThresholdCriticalMB
                };
                UpdateOverallStatus(response, memoryResult.Status);

                // Проверка диска
                var diskResult = _healthCheckService.CheckDisk();
                response.Services.Disk = new DiskHealthInfo
                {
                    Status = diskResult.Status.ToString(),
                    AvailableGB = diskResult.AvailableGB,
                    TotalGB = diskResult.TotalGB,
                    UsagePercent = Math.Round(diskResult.UsagePercent, 2),
                    ThresholdWarningGB = diskResult.ThresholdWarningGB,
                    ThresholdCriticalGB = diskResult.ThresholdCriticalGB
                };
                UpdateOverallStatus(response, diskResult.Status);

                sw.Stop();
                response.ResponseTimeMs = sw.ElapsedMilliseconds;

                // Кэшируем результат в production
                if (_environment.IsProduction())
                {
                    await CacheResultAsync(HealthCacheKey, response, cancellationToken);
                }

                return StatusCode(GetStatusCode(response.Status), response);
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(ex, "Health check failed");

                return StatusCode(StatusCodes.Status503ServiceUnavailable,
                    new HealthCheckResponse
                    {
                        Status = "Unhealthy",
                        Timestamp = DateTime.UtcNow,
                        Error = ex.Message,
                        ResponseTimeMs = sw.ElapsedMilliseconds
                    });
            }
        }

        /// <summary>
        /// Детальная проверка здоровья (расширенная)
        /// </summary>
        [HttpGet("detailed")]
        [AllowAnonymous]
        public async Task<IActionResult> GetDetailed(CancellationToken cancellationToken)
        {
            var sw = Stopwatch.StartNew();

            try
            {
                // Проверяем кэш в production
                if (_environment.IsProduction())
                {
                    var cached = await GetCachedResultAsync<DetailedHealthCheckResponse>(DetailedHealthCacheKey, cancellationToken);
                    if (cached != null)
                    {
                        cached.FromCache = true;
                        return Ok(cached);
                    }
                }

                var response = new DetailedHealthCheckResponse
                {
                    Timestamp = DateTime.UtcNow,
                    Version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "Unknown",
                    Environment = _environment.EnvironmentName,
                    Status = "Healthy",
                    FromCache = false
                };

                // Параллельная проверка всех сервисов
                var dbTask = _healthCheckService.CheckDatabaseAsync(cancellationToken);
                var redisTask = _healthCheckService.CheckRedisAsync(cancellationToken);
                var externalTask = _healthCheckService.CheckExternalDependenciesAsync(cancellationToken);
                var backgroundTask = _healthCheckService.GetBackgroundServicesStatusAsync(cancellationToken);

                await Task.WhenAll(dbTask, redisTask, externalTask, backgroundTask);

                // Database
                var dbResult = await dbTask;
                response.Database = MapToServiceDetail(dbResult);
                UpdateOverallStatus(response, dbResult.Status);

                // Redis
                var redisResult = await redisTask;
                response.Redis = MapToServiceDetail(redisResult);
                UpdateOverallStatus(response, redisResult.Status);

                // External Dependencies
                var externalResults = await externalTask;
                response.ExternalDependencies = externalResults.Select(r => new ExternalDependencyInfo
                {
                    Name = r.Name,
                    Status = r.Status.ToString(),
                    Message = r.Message,
                    StatusCode = r.StatusCode,
                    ResponseTimeMs = r.ResponseTimeMs
                }).ToList();

                foreach (var ext in externalResults)
                {
                    UpdateOverallStatus(response, ext.Status);
                }

                // Background Services
                var bgResults = await backgroundTask;
                response.BackgroundServices = bgResults.Select(r => new BackgroundServiceInfo
                {
                    Name = r.Name,
                    Status = r.Status.ToString(),
                    Message = r.Message,
                    IsRunning = r.IsRunning
                }).ToList();

                foreach (var bg in bgResults)
                {
                    UpdateOverallStatus(response, bg.Status);
                }

                // Memory & Disk
                var memoryResult = _healthCheckService.CheckMemory();
                response.Memory = new DetailedMemoryInfo
                {
                    Status = memoryResult.Status.ToString(),
                    UsageMB = memoryResult.UsageMB,
                    Gen0Collections = memoryResult.Gen0Collections,
                    Gen1Collections = memoryResult.Gen1Collections,
                    Gen2Collections = memoryResult.Gen2Collections,
                    Thresholds = new ThresholdInfo
                    {
                        WarningMB = memoryResult.ThresholdWarningMB,
                        CriticalMB = memoryResult.ThresholdCriticalMB
                    }
                };
                UpdateOverallStatus(response, memoryResult.Status);

                var diskResult = _healthCheckService.CheckDisk();
                response.Disk = new DetailedDiskInfo
                {
                    Status = diskResult.Status.ToString(),
                    AvailableGB = diskResult.AvailableGB,
                    TotalGB = diskResult.TotalGB,
                    UsagePercent = Math.Round(diskResult.UsagePercent, 2),
                    Thresholds = new DiskThresholdInfo
                    {
                        WarningGB = diskResult.ThresholdWarningGB,
                        CriticalGB = diskResult.ThresholdCriticalGB
                    }
                };
                UpdateOverallStatus(response, diskResult.Status);

                // System Info
                response.System = new SystemInfo
                {
                    UpTime = TimeSpan.FromMilliseconds(Environment.TickCount64),
                    Processors = Environment.ProcessorCount,
                    DotNetVersion = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
                    OSDescription = System.Runtime.InteropServices.RuntimeInformation.OSDescription
                };

                // Configuration Info
                response.Configuration = new ConfigurationInfo
                {
                    CacheDurationSeconds = _config.CacheDurationSeconds,
                    Thresholds = new AllThresholdsInfo
                    {
                        MemoryWarningMB = _config.Thresholds.MemoryWarningMB,
                        MemoryCriticalMB = _config.Thresholds.MemoryCriticalMB,
                        DiskSpaceWarningGB = _config.Thresholds.DiskSpaceWarningGB,
                        DiskSpaceCriticalGB = _config.Thresholds.DiskSpaceCriticalGB,
                        ResponseTimeWarningMs = _config.Thresholds.ResponseTimeWarningMs,
                        ResponseTimeCriticalMs = _config.Thresholds.ResponseTimeCriticalMs
                    }
                };

                sw.Stop();
                response.ResponseTimeMs = sw.ElapsedMilliseconds;

                // Кэшируем в production
                if (_environment.IsProduction())
                {
                    await CacheResultAsync(DetailedHealthCacheKey, response, cancellationToken);
                }

                return Ok(response);
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(ex, "Detailed health check failed");
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                {
                    Status = "Unhealthy",
                    Error = ex.Message,
                    ResponseTimeMs = sw.ElapsedMilliseconds
                });
            }
        }

        /// <summary>
        /// Prometheus-совместимые метрики
        /// </summary>
        [HttpGet("metrics")]
        [AllowAnonymous]
        [Produces("text/plain")]
        public async Task<IActionResult> GetMetrics(CancellationToken cancellationToken)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# HELP app_health_status Application health status (1=healthy, 0.5=degraded, 0=unhealthy)");
            sb.AppendLine("# TYPE app_health_status gauge");

            try
            {
                // Database
                var dbResult = await _healthCheckService.CheckDatabaseAsync(cancellationToken);
                sb.AppendLine($"app_health_status{{service=\"database\"}} {GetPrometheusValue(dbResult.Status)}");
                sb.AppendLine($"app_response_time_ms{{service=\"database\"}} {dbResult.ResponseTimeMs}");

                // Redis
                var redisResult = await _healthCheckService.CheckRedisAsync(cancellationToken);
                sb.AppendLine($"app_health_status{{service=\"redis\"}} {GetPrometheusValue(redisResult.Status)}");
                sb.AppendLine($"app_response_time_ms{{service=\"redis\"}} {redisResult.ResponseTimeMs}");

                // Memory
                var memoryResult = _healthCheckService.CheckMemory();
                sb.AppendLine();
                sb.AppendLine("# HELP app_memory_usage_mb Memory usage in megabytes");
                sb.AppendLine("# TYPE app_memory_usage_mb gauge");
                sb.AppendLine($"app_memory_usage_mb {memoryResult.UsageMB}");
                sb.AppendLine($"app_health_status{{service=\"memory\"}} {GetPrometheusValue(memoryResult.Status)}");
                sb.AppendLine();
                sb.AppendLine("# HELP app_gc_collections GC collection count by generation");
                sb.AppendLine("# TYPE app_gc_collections counter");
                sb.AppendLine($"app_gc_collections{{generation=\"0\"}} {memoryResult.Gen0Collections}");
                sb.AppendLine($"app_gc_collections{{generation=\"1\"}} {memoryResult.Gen1Collections}");
                sb.AppendLine($"app_gc_collections{{generation=\"2\"}} {memoryResult.Gen2Collections}");

                // Disk
                var diskResult = _healthCheckService.CheckDisk();
                sb.AppendLine();
                sb.AppendLine("# HELP app_disk_available_gb Available disk space in gigabytes");
                sb.AppendLine("# TYPE app_disk_available_gb gauge");
                sb.AppendLine($"app_disk_available_gb {diskResult.AvailableGB}");
                sb.AppendLine($"app_disk_total_gb {diskResult.TotalGB}");
                sb.AppendLine($"app_disk_usage_percent {diskResult.UsagePercent:F2}");
                sb.AppendLine($"app_health_status{{service=\"disk\"}} {GetPrometheusValue(diskResult.Status)}");

                // External Dependencies
                var externalResults = await _healthCheckService.CheckExternalDependenciesAsync(cancellationToken);
                if (externalResults.Any())
                {
                    sb.AppendLine();
                    sb.AppendLine("# HELP app_external_dependency_status External dependency status");
                    sb.AppendLine("# TYPE app_external_dependency_status gauge");
                    foreach (var ext in externalResults)
                    {
                        sb.AppendLine($"app_external_dependency_status{{name=\"{ext.Name}\"}} {GetPrometheusValue(ext.Status)}");
                        sb.AppendLine($"app_external_dependency_response_time_ms{{name=\"{ext.Name}\"}} {ext.ResponseTimeMs}");
                    }
                }

                // Background Services
                var bgResults = await _healthCheckService.GetBackgroundServicesStatusAsync(cancellationToken);
                if (bgResults.Any())
                {
                    sb.AppendLine();
                    sb.AppendLine("# HELP app_background_service_status Background service status");
                    sb.AppendLine("# TYPE app_background_service_status gauge");
                    foreach (var bg in bgResults)
                    {
                        sb.AppendLine($"app_background_service_status{{name=\"{bg.Name}\"}} {GetPrometheusValue(bg.Status)}");
                        sb.AppendLine($"app_background_service_running{{name=\"{bg.Name}\"}} {(bg.IsRunning ? 1 : 0)}");
                    }
                }

                // Uptime
                sb.AppendLine();
                sb.AppendLine("# HELP app_uptime_seconds Application uptime in seconds");
                sb.AppendLine("# TYPE app_uptime_seconds counter");
                sb.AppendLine($"app_uptime_seconds {Environment.TickCount64 / 1000}");

                // Thresholds
                sb.AppendLine();
                sb.AppendLine("# HELP app_threshold_memory_warning_mb Memory warning threshold in MB");
                sb.AppendLine("# TYPE app_threshold_memory_warning_mb gauge");
                sb.AppendLine($"app_threshold_memory_warning_mb {_config.Thresholds.MemoryWarningMB}");
                sb.AppendLine($"app_threshold_memory_critical_mb {_config.Thresholds.MemoryCriticalMB}");
                sb.AppendLine($"app_threshold_disk_warning_gb {_config.Thresholds.DiskSpaceWarningGB}");
                sb.AppendLine($"app_threshold_disk_critical_gb {_config.Thresholds.DiskSpaceCriticalGB}");

                return Content(sb.ToString(), "text/plain; version=0.0.4; charset=utf-8");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Metrics generation failed");
                sb.Clear();
                sb.AppendLine("# HELP app_health_status Application health status");
                sb.AppendLine("# TYPE app_health_status gauge");
                sb.AppendLine("app_health_status{service=\"overall\"} 0");
                return Content(sb.ToString(), "text/plain; version=0.0.4; charset=utf-8");
            }
        }

        /// <summary>
        /// Liveness probe для Kubernetes
        /// </summary>
        [HttpGet("live")]
        [AllowAnonymous]
        public IActionResult GetLive()
        {
            return Ok(new { Status = "Alive", Timestamp = DateTime.UtcNow });
        }

        /// <summary>
        /// Readiness probe для Kubernetes
        /// </summary>
        [HttpGet("ready")]
        [AllowAnonymous]
        public async Task<IActionResult> GetReady(CancellationToken cancellationToken)
        {
            try
            {
                var dbResult = await _healthCheckService.CheckDatabaseAsync(cancellationToken);

                if (dbResult.Status == HealthStatus.Unhealthy)
                {
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                    {
                        Status = "NotReady",
                        Reason = "Database connection failed",
                        Timestamp = DateTime.UtcNow
                    });
                }

                return Ok(new { Status = "Ready", Timestamp = DateTime.UtcNow });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Readiness check failed");
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                {
                    Status = "NotReady",
                    Reason = ex.Message,
                    Timestamp = DateTime.UtcNow
                });
            }
        }

        #region Helper Methods

        private async Task<T?> GetCachedResultAsync<T>(string key, CancellationToken cancellationToken) where T : class
        {
            try
            {
                var cached = await _cache.GetStringAsync(key, cancellationToken);
                if (!string.IsNullOrEmpty(cached))
                {
                    return JsonSerializer.Deserialize<T>(cached);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get cached health result");
            }
            return null;
        }

        private async Task CacheResultAsync<T>(string key, T result, CancellationToken cancellationToken)
        {
            try
            {
                var options = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(_config.CacheDurationSeconds)
                };
                var json = JsonSerializer.Serialize(result);
                await _cache.SetStringAsync(key, json, options, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cache health result");
            }
        }

        private static int GetStatusCode(string status) => status switch
        {
            "Healthy" => StatusCodes.Status200OK,
            "Degraded" => StatusCodes.Status200OK,
            _ => StatusCodes.Status503ServiceUnavailable
        };

        private static string GetPrometheusValue(HealthStatus status) => status switch
        {
            HealthStatus.Healthy => "1",
            HealthStatus.Degraded => "0.5",
            HealthStatus.Unhealthy => "0",
            _ => "0"
        };

        private static void UpdateOverallStatus(HealthCheckResponse response, HealthStatus status)
        {
            if (status == HealthStatus.Unhealthy)
                response.Status = "Unhealthy";
            else if (status == HealthStatus.Degraded && response.Status == "Healthy")
                response.Status = "Degraded";
        }

        private static void UpdateOverallStatus(DetailedHealthCheckResponse response, HealthStatus status)
        {
            if (status == HealthStatus.Unhealthy)
                response.Status = "Unhealthy";
            else if (status == HealthStatus.Degraded && response.Status == "Healthy")
                response.Status = "Degraded";
        }

        private static ServiceStatusDetail MapToServiceDetail(HealthCheckResult result) => new()
        {
            Status = result.Status.ToString(),
            Message = result.Message,
            ResponseTimeMs = result.ResponseTimeMs,
            Metrics = result.Metrics
        };

        #endregion

        #region Response Models

        public class HealthCheckResponse
        {
            public string Status { get; set; } = "Unknown";
            public DateTime Timestamp { get; set; }
            public ServiceStatus? Services { get; set; }
            public string? Error { get; set; }
            public long ResponseTimeMs { get; set; }
            public bool FromCache { get; set; }
        }

        public class ServiceStatus
        {
            public ServiceHealthInfo? Database { get; set; }
            public ServiceHealthInfo? Redis { get; set; }
            public MemoryHealthInfo? Memory { get; set; }
            public DiskHealthInfo? Disk { get; set; }
        }

        public class ServiceHealthInfo
        {
            public string Status { get; set; } = "Unknown";
            public long ResponseTimeMs { get; set; }
            public string? Message { get; set; }
        }

        public class MemoryHealthInfo
        {
            public string Status { get; set; } = "Unknown";
            public long UsageMB { get; set; }
            public long ThresholdWarningMB { get; set; }
            public long ThresholdCriticalMB { get; set; }
        }

        public class DiskHealthInfo
        {
            public string Status { get; set; } = "Unknown";
            public long AvailableGB { get; set; }
            public long TotalGB { get; set; }
            public double UsagePercent { get; set; }
            public long ThresholdWarningGB { get; set; }
            public long ThresholdCriticalGB { get; set; }
        }

        public class DetailedHealthCheckResponse
        {
            public string Status { get; set; } = "Unknown";
            public DateTime Timestamp { get; set; }
            public string? Version { get; set; }
            public string? Environment { get; set; }
            public long ResponseTimeMs { get; set; }
            public bool FromCache { get; set; }
            public ServiceStatusDetail? Database { get; set; }
            public ServiceStatusDetail? Redis { get; set; }
            public List<ExternalDependencyInfo>? ExternalDependencies { get; set; }
            public List<BackgroundServiceInfo>? BackgroundServices { get; set; }
            public DetailedMemoryInfo? Memory { get; set; }
            public DetailedDiskInfo? Disk { get; set; }
            public SystemInfo? System { get; set; }
            public ConfigurationInfo? Configuration { get; set; }
        }

        public class ServiceStatusDetail
        {
            public string Status { get; set; } = "Unknown";
            public string? Message { get; set; }
            public long ResponseTimeMs { get; set; }

            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public object? Metrics { get; set; }
        }

        public class ExternalDependencyInfo
        {
            public string Name { get; set; } = string.Empty;
            public string Status { get; set; } = "Unknown";
            public string? Message { get; set; }
            public int? StatusCode { get; set; }
            public long ResponseTimeMs { get; set; }
        }

        public class BackgroundServiceInfo
        {
            public string Name { get; set; } = string.Empty;
            public string Status { get; set; } = "Unknown";
            public string? Message { get; set; }
            public bool IsRunning { get; set; }
        }

        public class DetailedMemoryInfo
        {
            public string Status { get; set; } = "Unknown";
            public long UsageMB { get; set; }
            public int Gen0Collections { get; set; }
            public int Gen1Collections { get; set; }
            public int Gen2Collections { get; set; }
            public ThresholdInfo? Thresholds { get; set; }
        }

        public class ThresholdInfo
        {
            public long WarningMB { get; set; }
            public long CriticalMB { get; set; }
        }

        public class DetailedDiskInfo
        {
            public string Status { get; set; } = "Unknown";
            public long AvailableGB { get; set; }
            public long TotalGB { get; set; }
            public double UsagePercent { get; set; }
            public DiskThresholdInfo? Thresholds { get; set; }
        }

        public class DiskThresholdInfo
        {
            public long WarningGB { get; set; }
            public long CriticalGB { get; set; }
        }

        public class SystemInfo
        {
            public TimeSpan UpTime { get; set; }
            public int Processors { get; set; }
            public string? DotNetVersion { get; set; }
            public string? OSDescription { get; set; }
        }

        public class ConfigurationInfo
        {
            public int CacheDurationSeconds { get; set; }
            public AllThresholdsInfo? Thresholds { get; set; }
        }

        public class AllThresholdsInfo
        {
            public long MemoryWarningMB { get; set; }
            public long MemoryCriticalMB { get; set; }
            public long DiskSpaceWarningGB { get; set; }
            public long DiskSpaceCriticalGB { get; set; }
            public int ResponseTimeWarningMs { get; set; }
            public int ResponseTimeCriticalMs { get; set; }
        }

        #endregion
    }
}
