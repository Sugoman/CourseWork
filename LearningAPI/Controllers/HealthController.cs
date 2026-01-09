using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using LearningTrainer.Context;

namespace LearningAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HealthController : ControllerBase
    {
        private readonly ApiDbContext _context;
        private readonly ILogger<HealthController> _logger;

        public HealthController(ApiDbContext context, ILogger<HealthController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Проверка состояния API
        /// </summary>
        /// <returns>Статус здоровья приложения</returns>
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Get()
        {
            try
            {
                var response = new HealthCheckResponse
                {
                    Status = "Healthy",
                    Timestamp = DateTime.UtcNow,
                    Services = new ServiceStatus()
                };

                // Проверка подключения к БД
                try
                {
                    var dbConnected = await _context.Database.CanConnectAsync();
                    response.Services.Database = dbConnected ? "Healthy" : "Unhealthy";
                    
                    if (!dbConnected)
                    {
                        response.Status = "Degraded";
                        _logger.LogWarning("Database connection failed");
                    }
                }
                catch (Exception ex)
                {
                    response.Services.Database = "Unhealthy";
                    response.Status = "Unhealthy";
                    _logger.LogError(ex, "Database health check failed");
                }

                // Проверка памяти
                var memoryUsage = GC.GetTotalMemory(false) / (1024 * 1024); // MB
                response.Services.Memory = $"{memoryUsage} MB";
                
                if (memoryUsage > 500) // Если память > 500 MB
                {
                    response.Status = "Degraded";
                    _logger.LogWarning("High memory usage: {MemoryUsage} MB", memoryUsage);
                }

                // Проверка дискового пространства
                var driveInfo = new DriveInfo(Path.GetPathRoot(Environment.CurrentDirectory));
                response.Services.DiskSpace = $"{driveInfo.AvailableFreeSpace / (1024 * 1024 * 1024)} GB available";

                if (driveInfo.AvailableFreeSpace < (1024 * 1024 * 1024)) // < 1GB
                {
                    response.Status = "Degraded";
                    _logger.LogWarning("Low disk space available");
                }

                var statusCode = response.Status == "Healthy" ? StatusCodes.Status200OK :
                                 response.Status == "Degraded" ? StatusCodes.Status200OK :
                                 StatusCodes.Status503ServiceUnavailable;

                return StatusCode(statusCode, response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Health check failed");
                return StatusCode(StatusCodes.Status503ServiceUnavailable, 
                    new HealthCheckResponse 
                    { 
                        Status = "Unhealthy",
                        Timestamp = DateTime.UtcNow,
                        Error = ex.Message
                    });
            }
        }

        /// <summary>
        /// Расширенная проверка здоровья (детальная)
        /// </summary>
        [HttpGet("detailed")]
        [AllowAnonymous]
        public async Task<IActionResult> GetDetailed()
        {
            var response = new DetailedHealthCheckResponse
            {
                Timestamp = DateTime.UtcNow,
                Version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "Unknown",
                Environment = System.Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"
            };

            // Базовая информация
            response.Status = "Healthy";

            // Проверка БД
            try
            {
                var canConnect = await _context.Database.CanConnectAsync();
                response.Database = new ServiceStatusDetail 
                { 
                    Status = canConnect ? "Healthy" : "Unhealthy",
                    Message = canConnect ? "Connected" : "Connection failed"
                };

                if (canConnect)
                {
                    var userCount = _context.Users.Count();
                    var dictionaryCount = _context.Dictionaries.Count();
                    response.Database.Metrics = new 
                    { 
                        users = userCount, 
                        dictionaries = dictionaryCount 
                    };
                }
            }
            catch (Exception ex)
            {
                response.Database = new ServiceStatusDetail 
                { 
                    Status = "Unhealthy", 
                    Message = ex.Message 
                };
                response.Status = "Unhealthy";
            }

            // Информация о системе
            response.System = new SystemInfo
            {
                UpTime = TimeSpan.FromMilliseconds(Environment.TickCount),
                Processors = Environment.ProcessorCount,
                MemoryUsageMB = GC.GetTotalMemory(false) / (1024 * 1024),
                DotNetVersion = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription
            };

            return Ok(response);
        }

        public class HealthCheckResponse
        {
            public string Status { get; set; }
            public DateTime Timestamp { get; set; }
            public ServiceStatus Services { get; set; }
            public string Error { get; set; }
        }

        public class ServiceStatus
        {
            public string Database { get; set; } = "Checking...";
            public string Memory { get; set; }
            public string DiskSpace { get; set; }
        }

        public class DetailedHealthCheckResponse
        {
            public string Status { get; set; }
            public DateTime Timestamp { get; set; }
            public string Version { get; set; }
            public string Environment { get; set; }
            public ServiceStatusDetail Database { get; set; }
            public SystemInfo System { get; set; }
        }

        public class ServiceStatusDetail
        {
            public string Status { get; set; }
            public string Message { get; set; }
            public object Metrics { get; set; }
        }

        public class SystemInfo
        {
            public TimeSpan UpTime { get; set; }
            public int Processors { get; set; }
            public long MemoryUsageMB { get; set; }
            public string DotNetVersion { get; set; }
        }
    }
}
