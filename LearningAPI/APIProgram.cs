using LearningTrainer;
using LearningTrainerShared.Context;
using LearningTrainerShared.Services;
using LearningAPI.Middleware;
using LearningAPI.Configuration;
using LearningAPI.Services;
using MediatR;
using LearningAPI.Features.Dictionaries.Queries.GetDictionaries;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Net.Http.Headers;
using Microsoft.OpenApi.Models;
using System.Text;
using System.Text.Json.Serialization;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// Настройка кодировки консоли для корректного вывода UTF-8
Console.OutputEncoding = Encoding.UTF8;

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

builder.Services.AddDbContext<ApiDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(GetDictionariesHandler).Assembly));

var redisConnectionString = builder.Configuration.GetConnectionString("Redis");

if (!string.IsNullOrEmpty(redisConnectionString))
{
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

builder.Services.AddScoped<TokenService>();
builder.Services.AddSingleton<ISpacedRepetitionService, SpacedRepetitionService>();
builder.Services.AddScoped<IDictionaryService, DictionaryService>();
builder.Services.AddHttpClient<LearningTrainer.Services.ExternalDictionaryService>();

// Асинхронная обработка транскрипций (Channel + BackgroundService)
builder.Services.AddSingleton<TranscriptionChannel>();
builder.Services.AddHostedService<TranscriptionBackgroundService>();

// Health Check конфигурация и сервис
builder.Services.Configure<HealthCheckConfiguration>(
    builder.Configuration.GetSection(HealthCheckConfiguration.SectionName));
builder.Services.AddSingleton<IHealthCheckService, HealthCheckService>();
builder.Services.AddHttpClient(); // Для проверки внешних зависимостей

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

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.Run();
