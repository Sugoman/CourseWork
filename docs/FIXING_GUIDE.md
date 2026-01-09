# ?? Руководство по исправлению критических ошибок

## Содержание
1. [Быстрый старт исправлений](#быстрый-старт)
2. [Исправление каждой критической ошибки](#исправления)
3. [Примеры кода](#примеры)
4. [Чек-лист](#чек-лист)

---

## ?? Быстрый старт

**Приоритет исправлений (сверху вниз):**

| # | Проблема | Сложность | Время | Критичность |
|---|----------|----------|-------|-------------|
| 1 | Жёсткий URL в ApiDataService | ?? Low | 5 мин | ?? High |
| 2 | Отсутствие валидации входных данных | ?? Medium | 20 мин | ?? High |
| 3 | CORS не сконфигурирован | ?? Low | 10 мин | ?? High |
| 4 | Null Reference в GetUserId | ?? Medium | 10 мин | ?? High |
| 5 | Дублирование TokenService | ?? Medium | 30 мин | ?? Medium |
| 6 | Race Condition в DictionarySharing | ?? Hard | 30 мин | ?? High |
| 7 | Отсутствие логирования | ?? Medium | 40 мин | ?? Medium |
| 8 | Утечка информации в Forbid | ?? Low | 15 мин | ?? Medium |
| 9 | Нет пагинации и N+1 проблемы | ?? Hard | 50 мин | ?? Medium |
| 10 | Отсутствие обработки исключений | ?? Medium | 30 мин | ?? Medium |

---

## ?? Исправления

### 1?? Жёсткий URL в ApiDataService

**Файл:** `LearningTrainer\Services\ApiDataService.cs`

**Текущий код:**
```csharp
public ApiDataService()
{
    _httpClient = new HttpClient
    {
        BaseAddress = new Uri("http://localhost:5077")  // ? Hardcoded!
    };
}
```

**Решение:** Использовать конфигурацию

```csharp
public class ApiDataService : IDataService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public ApiDataService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        
        var apiBaseUrl = configuration.GetValue<string>("Api:BaseUrl") 
            ?? "http://localhost:5077";
        _httpClient.BaseAddress = new Uri(apiBaseUrl);
    }
}
```

**Конфигурация (appsettings.json):**
```json
{
  "Api": {
    "BaseUrl": "http://localhost:5077"
  }
}
```

**В App.xaml.cs:**
```csharp
protected override void OnStartup(StartupEventArgs e)
{
    var config = new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
        .Build();

    var apiService = new ApiDataService(new HttpClient(), config);
    // ...
}
```

---

### 2?? Отсутствие валидации входных данных

**Проблема:** В контроллерах нет проверки null и пустых значений.

**Решение 1: Data Annotations**

```csharp
// LearningTrainerShared\Models\Features\Dictionaries\CreateDictionaryRequest.cs
using System.ComponentModel.DataAnnotations;

public class CreateDictionaryRequest
{
    [Required(ErrorMessage = "Имя словаря обязательно")]
    [StringLength(100, MinimumLength = 1, 
        ErrorMessage = "Имя должно быть от 1 до 100 символов")]
    public string Name { get; set; }

    [StringLength(500, ErrorMessage = "Описание не должно превышать 500 символов")]
    public string Description { get; set; }

    [Required(ErrorMessage = "Исходный язык обязателен")]
    [StringLength(50)]
    public string LanguageFrom { get; set; }

    [Required(ErrorMessage = "Целевой язык обязателен")]
    [StringLength(50)]
    public string LanguageTo { get; set; }
}
```

**Решение 2: FluentValidation (рекомендуется)**

Установить NuGet пакет:
```bash
dotnet add package FluentValidation
```

Создать валидатор:
```csharp
// LearningAPI\Validators\CreateDictionaryRequestValidator.cs
using FluentValidation;
using LearningTrainerShared.Models;

public class CreateDictionaryRequestValidator : AbstractValidator<CreateDictionaryRequest>
{
    public CreateDictionaryRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Имя словаря обязательно")
            .MaximumLength(100).WithMessage("Максимум 100 символов")
            .Matches(@"^[a-zA-Zа-яА-ЯЁё\s'-]+$")
            .WithMessage("Имя содержит недопустимые символы");

        RuleFor(x => x.LanguageFrom)
            .NotEmpty()
            .Length(2, 50);

        RuleFor(x => x.LanguageTo)
            .NotEmpty()
            .Length(2, 50);
    }
}
```

Регистрация в `APIProgram.cs`:
```csharp
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddFluentValidationClientsideAdapters();
builder.Services.AddValidatorsFromAssemblyContaining<CreateDictionaryRequestValidator>();
```

**В контроллере:**
```csharp
[HttpPost]
[Authorize]
public async Task<IActionResult> CreateDictionary(
    [FromBody] CreateDictionaryRequest request)
{
    if (!ModelState.IsValid)
    {
        return BadRequest(ModelState);
    }

    // ModelState.IsValid = true, данные валидны
    var dictionary = new Dictionary
    {
        Name = request.Name,
        Description = request.Description,
        LanguageFrom = request.LanguageFrom,
        LanguageTo = request.LanguageTo,
        UserId = GetUserId(),
        CreatedAt = DateTime.UtcNow
    };

    _context.Dictionaries.Add(dictionary);
    await _context.SaveChangesAsync();

    return Ok(new { message = "Словарь создан", id = dictionary.Id });
}
```

---

### 3?? CORS не сконфигурирован

**Файл:** `LearningAPI\APIProgram.cs`

**Текущий код:**
```csharp
app.UseCors();  // ? Пусто!
```

**Решение:**

```csharp
// В конфигурации сервисов:
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:5173", "https://yourdomain.com" };

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowLocalhost", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials()
              .WithExposedHeaders("Content-Disposition", "X-Total-Count");  // Для пагинации
    });

    // Для development:
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// В middleware:
var environment = app.Environment;
if (environment.IsDevelopment())
{
    app.UseCors("AllowAll");
}
else
{
    app.UseCors("AllowLocalhost");
}
```

**В appsettings.json:**
```json
{
  "Cors": {
    "AllowedOrigins": [
      "http://localhost:5173",
      "https://yourdomain.com"
    ]
  }
}
```

---

### 4?? Null Reference в GetUserId

**Файл:** `LearningAPI\Controllers\SharingController.cs`

**Текущий код:**
```csharp
private int GetUserId() => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
// ? NullReferenceException если клейм не найден!
```

**Решение:**

```csharp
private int GetUserId()
{
    var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
    if (userIdClaim == null || string.IsNullOrEmpty(userIdClaim.Value))
    {
        throw new UnauthorizedAccessException("User ID not found in claims");
    }

    if (!int.TryParse(userIdClaim.Value, out var userId))
    {
        throw new InvalidOperationException("User ID is not a valid integer");
    }

    return userId;
}
```

**Или создать базовый контроллер:**

```csharp
// LearningAPI\Controllers\BaseApiController.cs
[ApiController]
[Authorize]
public abstract class BaseApiController : ControllerBase
{
    protected int GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (claim == null || !int.TryParse(claim.Value, out var userId))
        {
            throw new UnauthorizedAccessException("Invalid user context");
        }
        return userId;
    }

    protected string GetUserRole()
    {
        return User.FindFirst(ClaimTypes.Role)?.Value ?? "User";
    }
}

// Все контроллеры наследуются:
public class SharingController : BaseApiController
{
    [HttpGet("dictionary/{dictionaryId}/status")]
    public async Task<IActionResult> GetDictionarySharingStatus(int dictionaryId)
    {
        var teacherId = GetUserId();  // Безопасно!
        // ...
    }
}
```

---

### 5?? Race Condition в DictionarySharing

**Файл:** `LearningAPI\Controllers\SharingController.cs`

**Текущий код:**
```csharp
// ? Two-step операция без защиты
var sharingEntry = await _context.DictionarySharings
    .FirstOrDefaultAsync(ds =>
        ds.DictionaryId == request.ContentId &&
        ds.StudentId == request.StudentId);

if (sharingEntry == null)
{
    _context.DictionarySharings.Add(newEntry);
}
else
{
    _context.DictionarySharings.Remove(sharingEntry);
}
await _context.SaveChangesAsync();
```

**Проблема:** Между `FirstOrDefaultAsync` и `SaveChangesAsync` другой процесс может добавить запись.

**Решение 1: Уникальное ограничение (Primary Key)**

```csharp
// LearningTrainerShared\Context\ApiDbContext.cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    // ... existing code ...

    modelBuilder.Entity<DictionarySharing>()
        .HasKey(ds => new { ds.DictionaryId, ds.StudentId });  // Composite key

    // Или если есть Id:
    modelBuilder.Entity<DictionarySharing>()
        .HasAlternateKey(ds => new { ds.DictionaryId, ds.StudentId });
}
```

**Решение 2: UPSERT через EF Core**

```csharp
[HttpPost("dictionary/toggle")]
public async Task<IActionResult> ToggleDictionarySharing(
    [FromBody] ToggleSharingRequest request)
{
    var teacherId = GetUserId();

    var sharingEntry = await _context.DictionarySharings
        .FirstOrDefaultAsync(ds =>
            ds.DictionaryId == request.ContentId &&
            ds.StudentId == request.StudentId);

    if (sharingEntry == null)
    {
        var newEntry = new DictionarySharing
        {
            DictionaryId = request.ContentId,
            StudentId = request.StudentId,
            SharedAt = DateTime.UtcNow
        };
        _context.DictionarySharings.Add(newEntry);
        try
        {
            await _context.SaveChangesAsync();
            return Ok(new { Message = "Доступ предоставлен", Status = "Shared" });
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("Violation of PRIMARY KEY") ?? false)
        {
            return Conflict(new { Message = "Словарь уже распределён этому студенту" });
        }
    }
    else
    {
        _context.DictionarySharings.Remove(sharingEntry);
        await _context.SaveChangesAsync();
        return Ok(new { Message = "Доступ отозван", Status = "Unshared" });
    }
}
```

**Решение 3: SQL MERGE для истинного UPSERT**

```csharp
// Использовать FromSqlRaw если нужна максимальная производительность
await _context.Database.ExecuteSqlAsync(
    @"MERGE INTO DictionarySharings AS target
      USING (VALUES ({0}, {1}, {2})) AS source (DictionaryId, StudentId, SharedAt)
      ON target.DictionaryId = source.DictionaryId
         AND target.StudentId = source.StudentId
      WHEN MATCHED THEN DELETE
      WHEN NOT MATCHED THEN INSERT (DictionaryId, StudentId, SharedAt)
        VALUES (source.DictionaryId, source.StudentId, source.SharedAt);",
    request.ContentId, request.StudentId, DateTime.UtcNow);
```

---

### 6?? Дублирование TokenService

**Проблема:**
- `LearningTrainer\Services\TokenService.cs`
- `LearningAPI\Services\TokenService.cs`

**Решение:** Создать один в `LearningTrainerShared`

**1. Удалить оба файла**

**2. Создать единый:** `LearningTrainerShared\Services\TokenService.cs`

```csharp
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using LearningTrainerShared.Models;
using Microsoft.Extensions.Configuration;

namespace LearningTrainerShared.Services
{
    public class TokenService
    {
        private readonly IConfiguration _configuration;

        public TokenService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string GenerateAccessToken(User user)
        {
            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Login),
                new Claim(ClaimTypes.Role, user.Role?.Name ?? "User")
            };

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(1),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public string GenerateRefreshToken()
        {
            var randomNumber = new byte[32];
            using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomNumber);
                return Convert.ToBase64String(randomNumber);
            }
        }

        public ClaimsPrincipal GetPrincipalFromExpiredToken(string token)
        {
            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));

            var tokenValidationParameters = new TokenValidationParameters
            {
                ValidateAudience = false,
                ValidateIssuer = false,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateLifetime = false
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out SecurityToken securityToken);

            if (!(securityToken is JwtSecurityToken jwtSecurityToken) ||
                !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256,
                    StringComparison.InvariantCultureIgnoreCase))
            {
                throw new SecurityTokenException("Invalid token");
            }

            return principal;
        }
    }
}
```

**3. В APIProgram.cs:**
```csharp
// Изменить:
builder.Services.AddScoped<LearningTrainer.Services.TokenService>();

// На:
builder.Services.AddScoped<LearningTrainerShared.Services.TokenService>();
```

**4. В контроллерах используйте через DI:**
```csharp
public class AuthController : ControllerBase
{
    private readonly TokenService _tokenService;

    public AuthController(TokenService tokenService)
    {
        _tokenService = tokenService;
    }
}
```

---

### 7?? Отсутствие логирования

**Файл:** `LearningAPI\APIProgram.cs`

**Решение:**

```csharp
// Добавить Serilog
dotnet add package Serilog.AspNetCore
dotnet add package Serilog.Sinks.File
dotnet add package Serilog.Sinks.Console
```

**Конфигурация:**
```csharp
// APIProgram.cs
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Сириков логирование
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .WriteTo.File(
        path: "logs/log-.txt",
        rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

builder.Host.UseSerilog();

// ... rest of configuration ...

var app = builder.Build();

// Добавить LoggingMiddleware
app.UseSerilogRequestLogging();
```

**В контроллерах:**
```csharp
public class DictionaryController : BaseApiController
{
    private readonly ILogger<DictionaryController> _logger;

    public DictionaryController(ILogger<DictionaryController> logger, ApiDbContext context)
    {
        _logger = logger;
        // ...
    }

    [HttpPost]
    public async Task<IActionResult> CreateDictionary(
        [FromBody] CreateDictionaryRequest request)
    {
        try
        {
            _logger.LogInformation("Creating dictionary: {@Request}", request);
            
            var userId = GetUserId();
            var dictionary = new Dictionary { /* ... */ };

            _context.Dictionaries.Add(dictionary);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Dictionary created with ID {DictionaryId}", dictionary.Id);
            return Ok(dictionary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating dictionary for user {UserId}", GetUserId());
            return StatusCode(500, new { message = "Внутренняя ошибка сервера" });
        }
    }
}
```

---

### 8?? Утечка информации в Forbid

**Файл:** `LearningAPI\Controllers\SharingController.cs`

**Текущий код:**
```csharp
// ? Раскрывает информацию
return Forbid("Словарь не найден или не принадлежит вам.");
```

**Решение:**

```csharp
// ? Обобщённый ответ
if (!dictionaryExists)
{
    _logger.LogWarning("User {UserId} tried to access unauthorized dictionary {DictionaryId}",
        teacherId, dictionaryId);
    return NotFound();  // или Forbid() без сообщения
}

// Или вернуть 403:
return StatusCode(StatusCodes.Status403Forbidden,
    new { message = "Доступ запрещён" });
```

**Best Practice:**
```csharp
private IActionResult UnauthorizedAction(string action)
{
    _logger.LogWarning("Unauthorized action: {Action} by user {UserId}",
        action, GetUserId());
    
    // Всегда возвращать одинаковый ответ
    return StatusCode(StatusCodes.Status403Forbidden);
}

// Использование:
if (!dictionaryExists)
{
    return UnauthorizedAction("AccessDictionary");
}
```

---

### 9?? N+1 и отсутствие пагинации

**Проблема:**
```csharp
// ? N+1 и нет лимита
public async Task<IActionResult> GetDictionaries()
{
    var dictionaries = await _context.Dictionaries.ToListAsync();
    // Дальше может быть цикл, который будет вызывать доп. queries
}
```

**Решение:**

```csharp
[HttpGet]
[Authorize]
public async Task<IActionResult> GetDictionaries(
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 10,
    [FromQuery] string orderBy = "CreatedAt",
    [FromQuery] bool descending = true)
{
    const int maxPageSize = 100;
    if (pageSize > maxPageSize)
        pageSize = maxPageSize;

    if (page < 1)
        page = 1;

    var userId = GetUserId();

    var query = _context.Dictionaries
        .Where(d => d.UserId == userId)
        .Include(d => d.Words)  // ? Предотвращает N+1
        .Include(d => d.CreatedBy)
        .AsNoTracking();  // Если не нужны изменения

    // Сортировка
    query = orderBy switch
    {
        "Name" => descending ? query.OrderByDescending(d => d.Name) : query.OrderBy(d => d.Name),
        "CreatedAt" => descending ? query.OrderByDescending(d => d.CreatedAt) : query.OrderBy(d => d.CreatedAt),
        _ => query.OrderByDescending(d => d.CreatedAt)
    };

    var total = await query.CountAsync();
    var dictionaries = await query
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .ToListAsync();

    return Ok(new
    {
        data = dictionaries,
        pagination = new
        {
            page,
            pageSize,
            total,
            pageCount = (int)Math.Ceiling(total / (double)pageSize)
        }
    });
}
```

**В Response Header:**
```csharp
Response.Headers.Add("X-Total-Count", total.ToString());
Response.Headers.Add("X-Page-Size", pageSize.ToString());
```

---

### ?? Обработка исключений

**Решение 1: Global Exception Handler Middleware**

```csharp
// LearningAPI\Middleware\ExceptionHandlingMiddleware.cs
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception occurred");
            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var response = new ErrorResponse();

        switch (exception)
        {
            case UnauthorizedAccessException:
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                response.Message = "Не авторизован";
                break;
            case KeyNotFoundException:
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                response.Message = "Ресурс не найден";
                break;
            case InvalidOperationException:
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                response.Message = exception.Message;
                break;
            default:
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                response.Message = "Внутренняя ошибка сервера";
                break;
        }

        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var json = JsonSerializer.Serialize(response, options);
        return context.Response.WriteAsync(json);
    }

    public class ErrorResponse
    {
        public string Message { get; set; }
        public List<string> Errors { get; set; } = new();
    }
}
```

**Регистрация в APIProgram.cs:**
```csharp
app.UseMiddleware<ExceptionHandlingMiddleware>();

// ИЛИ использовать встроенное:
app.UseExceptionHandler(exceptionHandlerApp =>
{
    exceptionHandlerApp.Run(async context =>
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";

        var exceptionHandlerPathFeature = context.Features.Get<IExceptionHandlerPathFeature>();
        var exception = exceptionHandlerPathFeature?.Error;

        var errorResponse = new
        {
            message = "Произошла ошибка при обработке вашего запроса",
            details = app.Environment.IsDevelopment() ? exception?.Message : null
        };

        await context.Response.WriteAsJsonAsync(errorResponse);
    });
});
```

---

## ?? Чек-лист исправлений

### Фаза 1: Критическая безопасность (1-2 часа)
- [ ] ? Исправить жёсткий URL в ApiDataService ? конфигурация
- [ ] ? Добавить валидацию входных данных (FluentValidation)
- [ ] ? Настроить CORS политику
- [ ] ? Исправить Null Reference в GetUserId

### Фаза 2: Функциональность (2-3 часа)
- [ ] ? Слить TokenService в LearningTrainerShared
- [ ] ? Исправить Race Condition в DictionarySharing (UPSERT)
- [ ] ? Добавить пагинацию в список endpoints
- [ ] ? Добавить Include для N+1 проблем

### Фаза 3: Мониторинг (1-2 часа)
- [ ] ? Добавить логирование (Serilog)
- [ ] ? Добавить Global Exception Handler
- [ ] ? Исправить утечку информации в Forbid

### Фаза 4: Quality (3-4 часа)
- [ ] ? Создать BaseApiController
- [ ] ? Добавить XML комментарии
- [ ] ? Написать Unit тесты (XUnit)
- [ ] ? Написать Integration тесты

### Фаза 5: DevOps (опционально)
- [ ] ? Добавить Docker поддержку
- [ ] ? Настроить GitHub Actions CI/CD
- [ ] ? Добавить Healthcheck endpoint
- [ ] ? Application Insights

---

## ?? Дополнительные ресурсы

- [OWASP Top 10 - Безопасность](https://owasp.org/www-project-top-ten/)
- [Microsoft - REST API Best Practices](https://docs.microsoft.com/en-us/azure/architecture/best-practices/api-design)
- [Entity Framework Core - Performance](https://learn.microsoft.com/en-us/ef/core/performance/)
- [FluentValidation Docs](https://docs.fluentvalidation.net/)
- [Serilog Docs](https://serilog.net/)

