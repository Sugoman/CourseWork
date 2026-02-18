using LearningTrainer;
using LearningTrainerShared.Context;
using LearningTrainerShared.Services;
using LearningAPI.Extensions;
using LearningAPI.Middleware;
using LearningAPI.Configuration;
using LearningAPI.Services;
using LearningAPI.Validators;
using MediatR;
using LearningAPI.Features.Dictionaries.Queries.GetDictionaries;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Net.Http.Headers;
using Microsoft.OpenApi.Models;
using Serilog;
using StackExchange.Redis;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// Настройка кодировки консоли для корректного вывода UTF-8
Console.OutputEncoding = Encoding.UTF8;

// ===== Serilog: Structured Logging =====
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext} | {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        path: "logs/log-.txt",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} | {Message:lj}{NewLine}{Exception}")
    .WriteTo.Seq(
        serverUrl: context.Configuration["Seq:ServerUrl"] ?? "http://localhost:5341",
        restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Information));

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "Пожалуйста, введите 'Bearer' [пробел] и ваш токен",
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});

// EF Core Query Filters: привязка TenantUserId к текущему пользователю
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<LearningAPI.Services.ITenantProvider, LearningAPI.Services.HttpContextTenantProvider>();

builder.Services.AddDbContext<ApiDbContext>((sp, options) =>
{
    options.UseSqlServer(connectionString);
});

builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(GetDictionariesHandler).Assembly));

var redisConnectionString = builder.Configuration.GetConnectionString("Redis");

if (!string.IsNullOrEmpty(redisConnectionString))
{
    builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
        ConnectionMultiplexer.Connect(redisConnectionString));

    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConnectionString;
        options.InstanceName = "LearningTrainerCache_"; 
    });
}
else
{
    // Fallback to in-memory cache when Redis is not configured
    builder.Services.AddDistributedMemoryCache();
}

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Create(System.Text.Unicode.UnicodeRanges.All);
    });

// ===== FluentValidation =====
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<LoginRequestValidator>();

// ===== Rate Limiting (авторизация) =====
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Политика для эндпоинтов авторизации: 10 запросов / 60 сек на IP
    options.AddPolicy("auth", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromSeconds(60),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));

    // Глобальная политика: 100 запросов / 60 сек на IP
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromSeconds(60),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 2
            }));
});

// ===== API Versioning =====
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new Asp.Versioning.ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = Asp.Versioning.ApiVersionReader.Combine(
        new Asp.Versioning.QueryStringApiVersionReader("api-version"),
        new Asp.Versioning.HeaderApiVersionReader("X-Api-Version"));
}).AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

// ===== Response Compression (Brotli + Gzip) =====
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[]
    {
        "application/json",
        "text/plain",
        "text/csv"
    });
});
builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
    options.Level = System.IO.Compression.CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(options =>
    options.Level = System.IO.Compression.CompressionLevel.SmallestSize);

builder.Services.AddScoped<TokenService>();
builder.Services.AddSingleton<ISpacedRepetitionService, SpacedRepetitionService>();
builder.Services.AddScoped<IDictionaryService, DictionaryService>();
builder.Services.AddHttpClient<LearningTrainer.Services.ExternalDictionaryService>();

// Асинхронная обработка транскрипций (Channel + BackgroundService)
builder.Services.AddSingleton<TranscriptionChannel>();
builder.Services.AddHostedService<TranscriptionBackgroundService>();

// Health Check конфигурация и кастомный сервис
builder.Services.Configure<HealthCheckConfiguration>(
    builder.Configuration.GetSection(HealthCheckConfiguration.SectionName));
builder.Services.AddSingleton<LearningAPI.Services.IHealthCheckService, LearningAPI.Services.HealthCheckService>();
builder.Services.AddHttpClient(); // Для проверки внешних зависимостей

// ===== Standard Health Checks (SQL Server + Redis) =====
var healthChecksBuilder = builder.Services.AddHealthChecks()
    .AddSqlServer(
        connectionString ?? "invalid",
        name: "sqlserver",
        timeout: TimeSpan.FromSeconds(5),
        tags: new[] { "db", "critical" });

if (!string.IsNullOrEmpty(redisConnectionString))
{
    healthChecksBuilder.AddRedis(
        redisConnectionString,
        name: "redis",
        timeout: TimeSpan.FromSeconds(3),
        tags: new[] { "cache" });
}

// Statistics service
builder.Services.AddScoped<IStatisticsService, StatisticsService>();

// Добавить CORS конфигурацию
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { 
        "http://localhost:5173", 
        "http://localhost:3000", 
        "https://localhost:5001", 
        "http://localhost:5000",
        "https://localhost:57854",
        "http://localhost:57855"
    };

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowLocalhost", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials()
              .WithExposedHeaders("Content-Disposition", "X-Total-Count");
    });

    // Для development - разрешаем все
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var jwtKey = builder.Configuration["Jwt:Key"];

if (string.IsNullOrEmpty(jwtKey) || jwtKey == "SET_VIA_USER_SECRETS_OR_ENV")
    throw new InvalidOperationException(
        "Jwt:Key is not configured. Set it via: " +
        "1) Environment variable Jwt__Key (in docker-compose.yml), " +
        "2) dotnet user-secrets, or " +
        "3) appsettings.Development.json. " +
        "Generate a key with: openssl rand -hex 32");

if (Encoding.UTF8.GetByteCount(jwtKey) < 32)
    throw new InvalidOperationException(
        $"Jwt:Key must be at least 256 bits (32 bytes) for HS256. Current key is {Encoding.UTF8.GetByteCount(jwtKey) * 8} bits. " +
        "Generate a new key: openssl rand -hex 32");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

var app = builder.Build();

// Автоматическое применение pending-миграций при запуске
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
    var pendingMigrations = db.Database.GetPendingMigrations().ToList();
    if (pendingMigrations.Count > 0)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<ApiDbContext>>();
        logger.LogInformation("Applying {Count} pending migration(s): {Migrations}",
            pendingMigrations.Count, string.Join(", ", pendingMigrations));
        db.Database.Migrate();
        logger.LogInformation("All migrations applied successfully.");
    }
}

// Инициализация логгера для кэш-расширений
DistributedCacheExtensions.InitializeLogger(app.Services.GetRequiredService<ILoggerFactory>());

// Serilog request logging
app.UseSerilogRequestLogging(options =>
{
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
        diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.ToString());
    };
});

// Response Compression (перед остальным middleware)
app.UseResponseCompression();

app.UseSwagger();
app.UseSwaggerUI();

// Глобальная обработка исключений
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Использовать CORS
var environment = app.Environment;
if (environment.IsDevelopment())
{
    app.UseCors("AllowAll");
}
else
{
    app.UseCors("AllowLocalhost");
}

// Rate Limiting
app.UseRateLimiter();

app.UseAuthentication();

app.UseAuthorization();

// Multi-tenant: устанавливает TenantUserId на DbContext для EF Core Query Filters
app.UseMiddleware<TenantMiddleware>();

app.MapControllers();

// Стандартный Health Check endpoint
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var result = System.Text.Json.JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                duration = e.Value.Duration.TotalMilliseconds
            }),
            totalDuration = report.TotalDuration.TotalMilliseconds
        });
        await context.Response.WriteAsync(result);
    }
});

app.Run();
