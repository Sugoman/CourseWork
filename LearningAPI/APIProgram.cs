using LearningTrainer;
using LearningTrainer.Context;
using LearningTrainerShared.Services;
using LearningAPI.Middleware;
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
Console.WriteLine($"[Startup] Redis connection: {redisConnectionString ?? "NOT CONFIGURED"}");

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
    Console.WriteLine("[Startup] Using in-memory distributed cache (Redis not configured)");
    builder.Services.AddDistributedMemoryCache();
}

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
    });

builder.Services.AddScoped<TokenService>();
builder.Services.AddHttpClient<LearningTrainer.Services.ExternalDictionaryService>();

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
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
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
