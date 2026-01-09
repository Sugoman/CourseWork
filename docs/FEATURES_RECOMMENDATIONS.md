# ?? Рекомендации по добавлению новых функций

## Содержание
1. [Приоритизация функций](#приоритизация)
2. [Высокоприоритетные функции](#высокоприоритетные)
3. [Среднеприоритетные функции](#среднеприоритетные)
4. [Долгосрочные функции](#долгосрочные)
5. [Архитектурные улучшения](#архитектурные-улучшения)

---

## ?? Приоритизация

| Функция | Сложность | Время | Приоритет | Impact |
|---------|----------|-------|-----------|--------|
| **Роли и разрешения (RBAC)** | ?? Medium | 4ч | ?? High | 10/10 |
| **Refresh Token механизм** | ?? Medium | 3ч | ?? High | 9/10 |
| **Аудит действий (Logging)** | ?? Medium | 5ч | ?? High | 8/10 |
| **Синхронизация offline/online** | ?? Hard | 8ч | ?? Medium | 7/10 |
| **Real-time notifications (WebSocket)** | ?? Hard | 10ч | ?? Medium | 8/10 |
| **Экспорт/импорт словарей** | ?? Medium | 6ч | ?? Medium | 7/10 |
| **Произношение (Text-to-Speech)** | ?? Low | 2ч | ?? Medium | 6/10 |
| **Лидерборд / Сравнение статистики** | ?? Medium | 4ч | ?? Low | 6/10 |
| **Карточки (flashcards) с SRS** | ?? Hard | 12ч | ?? Low | 8/10 |
| **Mobile API / React Native** | ?? Hard | 40ч | ?? Low | 7/10 |

---

## ?? Высокоприоритетные функции

### 1. Role-Based Access Control (RBAC)

**Текущее состояние:** Роли существуют, но не используются в контроллерах.

**Что нужно сделать:**

```csharp
// 1. Создать constants для ролей
namespace LearningTrainerShared.Constants
{
    public static class UserRoles
    {
        public const string Admin = "Admin";
        public const string Teacher = "Teacher";
        public const string Student = "Student";
    }
}

// 2. Обновить контроллеры с [Authorize(Roles = ...)]
[ApiController]
[Route("api/dictionaries")]
public class DictionaryController : BaseApiController
{
    [HttpPost]
    [Authorize(Roles = UserRoles.Teacher)]  // Только учителя могут создавать
    public async Task<IActionResult> CreateDictionary(
        [FromBody] CreateDictionaryRequest request)
    {
        // ...
    }

    [HttpPost("{id}/share")]
    [Authorize(Roles = UserRoles.Teacher)]  // Только владелец может делиться
    public async Task<IActionResult> ShareDictionary(int id, [FromBody] ShareRequest request)
    {
        var userId = GetUserId();
        var dictionary = await _context.Dictionaries
            .FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId);

        if (dictionary == null)
            return NotFound();

        // Логика распределения
        return Ok();
    }
}

// 3. Добавить permissions middleware
[AttributeUsage(AttributeTargets.Method)]
public class RequirePermissionAttribute : Attribute
{
    public RequirePermissionAttribute(string permission) 
    {
        Permission = permission;
    }
    public string Permission { get; set; }
}

public class PermissionMiddleware
{
    private readonly RequestDelegate _next;

    public PermissionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IUserPermissionService permissionService)
    {
        // Проверка прав доступа
        await _next(context);
    }
}

// 4. Сервис для управления разрешениями
public interface IUserPermissionService
{
    Task<bool> HasPermissionAsync(int userId, string permission);
    Task<List<string>> GetUserPermissionsAsync(int userId);
}

public class UserPermissionService : IUserPermissionService
{
    private readonly ApiDbContext _context;

    public UserPermissionService(ApiDbContext context)
    {
        _context = context;
    }

    public async Task<bool> HasPermissionAsync(int userId, string permission)
    {
        var user = await _context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == userId);

        // Логика проверки прав в зависимости от роли
        return user?.Role?.Name switch
        {
            UserRoles.Admin => true,  // Admin имеет все права
            UserRoles.Teacher => permission.StartsWith("Teacher."),
            UserRoles.Student => permission.StartsWith("Student."),
            _ => false
        };
    }

    public async Task<List<string>> GetUserPermissionsAsync(int userId)
    {
        var user = await _context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == userId);

        return user?.Role?.Name switch
        {
            UserRoles.Admin => new()
            {
                "Admin.ManageUsers",
                "Admin.ViewLogs",
                "Teacher.CreateDictionary",
                "Teacher.ShareContent"
            },
            UserRoles.Teacher => new()
            {
                "Teacher.CreateDictionary",
                "Teacher.EditDictionary",
                "Teacher.ShareContent",
                "Teacher.ViewStudents"
            },
            _ => new() { "Student.View" }
        };
    }
}
```

---

### 2. Refresh Token механизм

**Текущее состояние:** Только AccessToken, нет refresh механизма.

**Что нужно добавить:**

```csharp
// 1. Добавить в User модель
public class User
{
    // ... existing fields ...
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiry { get; set; }
}

// 2. Миграция БД
// dotnet ef migrations add AddRefreshTokenFields

// 3. Обновить TokenService
public class TokenService
{
    // ... existing code ...

    public RefreshTokenResponse GenerateTokenPair(User user)
    {
        var accessToken = GenerateAccessToken(user);
        var refreshToken = GenerateRefreshToken();

        return new RefreshTokenResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresIn = 3600  // 1 час
        };
    }
}

// 4. AuthController - новый endpoint
[HttpPost("refresh")]
public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
{
    try
    {
        var principal = _tokenService.GetPrincipalFromExpiredToken(request.AccessToken);
        var userId = int.Parse(principal.FindFirst(ClaimTypes.NameIdentifier)?.Value);

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user?.RefreshToken != request.RefreshToken ||
            user?.RefreshTokenExpiry < DateTime.UtcNow)
        {
            return Unauthorized(new { message = "Refresh token invalid or expired" });
        }

        var tokenPair = _tokenService.GenerateTokenPair(user);

        // Сохранить новый refresh token в БД
        user.RefreshToken = tokenPair.RefreshToken;
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);
        await _context.SaveChangesAsync();

        return Ok(tokenPair);
    }
    catch (SecurityTokenException)
    {
        return BadRequest(new { message = "Invalid access token" });
    }
}

// 5. Login endpoint - сохраняет refresh token
[HttpPost("login")]
public async Task<IActionResult> Login([FromBody] LoginRequest request)
{
    var user = await _context.Users
        .Include(u => u.Role)
        .FirstOrDefaultAsync(u => u.Login == request.Username);

    if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        return Unauthorized();

    var tokenPair = _tokenService.GenerateTokenPair(user);

    // Сохранить refresh token
    user.RefreshToken = tokenPair.RefreshToken;
    user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);
    await _context.SaveChangesAsync();

    return Ok(tokenPair);
}
```

**DTO Models:**
```csharp
public class RefreshTokenRequest
{
    public string AccessToken { get; set; }
    public string RefreshToken { get; set; }
}

public class RefreshTokenResponse
{
    public string AccessToken { get; set; }
    public string RefreshToken { get; set; }
    public int ExpiresIn { get; set; }  // секунды
}
```

---

### 3. Аудит действий (Action Logging)

**Что нужно:**

```csharp
// 1. Entity для аудита
public class AuditLog
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Action { get; set; }  // "Create", "Update", "Delete"
    public string EntityType { get; set; }  // "Dictionary", "Word", "Rule"
    public int? EntityId { get; set; }
    public string OldValues { get; set; }  // JSON
    public string NewValues { get; set; }  // JSON
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string IpAddress { get; set; }

    public User User { get; set; }
}

// 2. DbContext update
public DbSet<AuditLog> AuditLogs { get; set; }

// 3. Сервис для логирования
public interface IAuditService
{
    Task LogActionAsync(int userId, string action, string entityType, 
        int? entityId, object oldValues = null, object newValues = null, 
        HttpContext context = null);
}

public class AuditService : IAuditService
{
    private readonly ApiDbContext _context;

    public async Task LogActionAsync(int userId, string action, string entityType,
        int? entityId, object oldValues = null, object newValues = null,
        HttpContext context = null)
    {
        var auditLog = new AuditLog
        {
            UserId = userId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            OldValues = oldValues != null ? JsonConvert.SerializeObject(oldValues) : null,
            NewValues = newValues != null ? JsonConvert.SerializeObject(newValues) : null,
            IpAddress = context?.Connection.RemoteIpAddress?.ToString(),
            CreatedAt = DateTime.UtcNow
        };

        _context.AuditLogs.Add(auditLog);
        await _context.SaveChangesAsync();
    }
}

// 4. Использование в контроллерах
[HttpPost]
[Authorize(Roles = UserRoles.Teacher)]
public async Task<IActionResult> CreateDictionary(
    [FromBody] CreateDictionaryRequest request)
{
    var userId = GetUserId();
    var dictionary = new Dictionary { /* ... */ };

    _context.Dictionaries.Add(dictionary);
    await _context.SaveChangesAsync();

    // Логирование
    await _auditService.LogActionAsync(
        userId: userId,
        action: "Create",
        entityType: "Dictionary",
        entityId: dictionary.Id,
        newValues: dictionary,
        context: HttpContext);

    return Ok(dictionary);
}

// 5. Endpoint для просмотра логов (только для Admin)
[HttpGet("audit-logs")]
[Authorize(Roles = UserRoles.Admin)]
public async Task<IActionResult> GetAuditLogs(
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 50,
    [FromQuery] string entityType = null,
    [FromQuery] string action = null)
{
    var query = _context.AuditLogs
        .Include(al => al.User)
        .AsQueryable();

    if (!string.IsNullOrEmpty(entityType))
        query = query.Where(al => al.EntityType == entityType);

    if (!string.IsNullOrEmpty(action))
        query = query.Where(al => al.Action == action);

    var total = await query.CountAsync();
    var logs = await query
        .OrderByDescending(al => al.CreatedAt)
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .ToListAsync();

    return Ok(new { data = logs, total, page, pageSize });
}
```

---

## ?? Среднеприоритетные функции

### 4. Синхронизация Offline/Online

**Идея:** Клиент работает offline, затем синхронизирует при подключении.

```csharp
// 1. Отслеживание синхронизации
public class SyncLog
{
    public int Id { get; set; }
    public string EntityType { get; set; }
    public int EntityId { get; set; }
    public string Action { get; set; }  // "Create", "Update", "Delete"
    public string Data { get; set; }  // JSON
    public bool IsSynced { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SyncedAt { get; set; }
}

// 2. В WPF клиенте - сохранение изменений
public class OfflineSyncService
{
    private readonly LocalDbContext _localContext;
    private readonly HttpClient _httpClient;

    public async Task SaveOfflineChangeAsync<T>(
        string entityType, int entityId, string action, T data)
    {
        var syncLog = new SyncLog
        {
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            Data = JsonConvert.SerializeObject(data),
            IsSynced = false
        };

        // Сохранить локально
        _localContext.SyncLogs.Add(syncLog);
        await _localContext.SaveChangesAsync();
    }

    public async Task SyncWithServerAsync()
    {
        var unsynced = _localContext.SyncLogs
            .Where(sl => !sl.IsSynced)
            .ToList();

        foreach (var log in unsynced)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync(
                    $"/api/sync/apply",
                    new { log.EntityType, log.Action, log.Data });

                if (response.IsSuccessStatusCode)
                {
                    log.IsSynced = true;
                    log.SyncedAt = DateTime.UtcNow;
                }
            }
            catch (HttpRequestException)
            {
                // Повторить позже
                continue;
            }
        }

        await _localContext.SaveChangesAsync();
    }
}

// 3. API endpoint для синхронизации
[HttpPost("sync/apply")]
[Authorize]
public async Task<IActionResult> ApplySync([FromBody] SyncRequest request)
{
    var userId = GetUserId();

    try
    {
        return request.EntityType switch
        {
            "Dictionary" when request.Action == "Create" =>
                await HandleCreateDictionary(userId, request),

            "Word" when request.Action == "Create" =>
                await HandleCreateWord(userId, request),

            "Word" when request.Action == "Delete" =>
                await HandleDeleteWord(userId, request),

            _ => BadRequest("Unknown action")
        };
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Sync error");
        return StatusCode(500);
    }
}
```

---

### 5. Real-time уведомления (WebSocket)

**Использовать:** SignalR

```bash
dotnet add package Microsoft.AspNetCore.SignalR
```

**Реализация:**
```csharp
// 1. Hub для уведомлений
public class NotificationHub : Hub
{
    private readonly ILogger<NotificationHub> _logger;

    public NotificationHub(ILogger<NotificationHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
        _logger.LogInformation("User {UserId} connected", userId);
        await base.OnConnectedAsync();
    }

    public async Task SendNotificationToUser(int userId, string message)
    {
        await Clients.Group($"user_{userId}")
            .SendAsync("ReceiveNotification", message);
    }
}

// 2. Регистрация в APIProgram.cs
builder.Services.AddSignalR();
// ...
app.MapHub<NotificationHub>("/hubs/notifications");

// 3. Использование в контроллерах
[HttpPost]
[Authorize(Roles = UserRoles.Teacher)]
public async Task<IActionResult> ShareDictionary(
    int dictionaryId,
    [FromBody] ShareRequest request)
{
    // ... логика ...

    var hubContext = HttpContext.RequestServices
        .GetRequiredService<IHubContext<NotificationHub>>();

    await hubContext.Clients.Group($"user_{request.StudentId}")
        .SendAsync("ReceiveNotification",
            new { type = "DictionaryShared", dictionaryId, message = "Новый словарь" });

    return Ok();
}

// 4. WPF клиент
public class NotificationService
{
    private HubConnection _hubConnection;

    public async Task InitializeAsync(string token)
    {
        _hubConnection = new HubConnectionBuilder()
            .WithUrl("http://localhost:5077/hubs/notifications",
                options => options.AccessTokenProvider = () => Task.FromResult(token))
            .WithAutomaticReconnect()
            .Build();

        _hubConnection.On<object>("ReceiveNotification",
            notification => HandleNotification(notification));

        await _hubConnection.StartAsync();
    }

    private void HandleNotification(object notification)
    {
        // Показать уведомление в UI
        // MainThread.BeginInvokeOnMainThread(() =>
        // {
        //     MessageBox.Show(notification.ToString());
        // });
    }
}
```

---

### 6. Экспорт/Импорт словарей

```csharp
// 1. Export в CSV
[HttpGet("{dictionaryId}/export/csv")]
[Authorize]
public async Task<IActionResult> ExportDictionaryAsCsv(int dictionaryId)
{
    var userId = GetUserId();
    var dictionary = await _context.Dictionaries
        .Include(d => d.Words)
        .FirstOrDefaultAsync(d => d.Id == dictionaryId && d.UserId == userId);

    if (dictionary == null)
        return NotFound();

    var csv = new StringBuilder();
    csv.AppendLine("Word,Translation,Example,DifficultyLevel");

    foreach (var word in dictionary.Words)
    {
        csv.AppendLine($"\"{word.OriginalWord}\",\"{word.Translation}\"," +
            $"\"{word.Example}\",\"{word.DifficultyLevel}\"");
    }

    var bytes = Encoding.UTF8.GetBytes(csv.ToString());
    return File(bytes, "text/csv", $"{dictionary.Name}_export.csv");
}

// 2. Export в Excel
[HttpGet("{dictionaryId}/export/excel")]
[Authorize]
public async Task<IActionResult> ExportDictionaryAsExcel(int dictionaryId)
{
    // Использовать EPPlus или ClosedXML
    // dotnet add package ClosedXML
    
    var userId = GetUserId();
    var dictionary = await _context.Dictionaries
        .Include(d => d.Words)
        .FirstOrDefaultAsync(d => d.Id == dictionaryId && d.UserId == userId);

    if (dictionary == null)
        return NotFound();

    using (var workbook = new XLWorkbook())
    {
        var worksheet = workbook.Worksheets.Add(dictionary.Name);

        // Headers
        worksheet.Cell("A1").Value = "Word";
        worksheet.Cell("B1").Value = "Translation";
        worksheet.Cell("C1").Value = "Example";

        // Data
        int row = 2;
        foreach (var word in dictionary.Words)
        {
            worksheet.Cell($"A{row}").Value = word.OriginalWord;
            worksheet.Cell($"B{row}").Value = word.Translation;
            worksheet.Cell($"C{row}").Value = word.Example;
            row++;
        }

        using (var stream = new MemoryStream())
        {
            workbook.SaveAs(stream);
            stream.Position = 0;
            return File(stream.ToArray(), 
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"{dictionary.Name}_export.xlsx");
        }
    }
}

// 3. Import из CSV
[HttpPost("import/csv")]
[Authorize(Roles = UserRoles.Teacher)]
public async Task<IActionResult> ImportDictionaryFromCsv(
    [FromQuery] int dictionaryId,
    IFormFile file)
{
    if (file?.Length == 0)
        return BadRequest("File is empty");

    var userId = GetUserId();
    var dictionary = await _context.Dictionaries
        .FirstOrDefaultAsync(d => d.Id == dictionaryId && d.UserId == userId);

    if (dictionary == null)
        return NotFound();

    using (var reader = new StreamReader(file.OpenReadStream()))
    {
        string line;
        bool isHeader = true;

        while ((line = await reader.ReadLineAsync()) != null)
        {
            if (isHeader)
            {
                isHeader = false;
                continue;
            }

            var parts = line.Split(',');
            if (parts.Length < 2)
                continue;

            var word = new Word
            {
                DictionaryId = dictionaryId,
                OriginalWord = parts[0].Trim('"'),
                Translation = parts[1].Trim('"'),
                Example = parts.Length > 2 ? parts[2].Trim('"') : null,
                CreatedAt = DateTime.UtcNow
            };

            _context.Words.Add(word);
        }
    }

    await _context.SaveChangesAsync();
    return Ok(new { message = "Import completed" });
}
```

---

### 7. Text-to-Speech (Произношение)

```csharp
// 1. Использовать Azure Cognitive Services или Google Cloud Speech
// dotnet add package Microsoft.CognitiveServices.Speech

[HttpGet("{wordId}/pronounce")]
[Authorize]
public async Task<IActionResult> GetPronunciation(int wordId)
{
    var word = await _context.Words.FirstOrDefaultAsync(w => w.Id == wordId);
    if (word == null)
        return NotFound();

    try
    {
        using (var speechConfig = SpeechConfig.FromSubscription(
            _configuration["AzureSpeech:Key"],
            _configuration["AzureSpeech:Region"]))
        {
            speechConfig.SpeechSynthesisVoiceName = "en-US-AriaNeural";

            using (var synthesizer = new SpeechSynthesizer(speechConfig, null))
            {
                var result = await synthesizer.SpeakTextAsync(word.OriginalWord);

                if (result.Reason == ResultReason.SynthesizingAudioCompleted)
                {
                    return File(result.AudioData, "audio/wav", $"{word.OriginalWord}.wav");
                }
                else
                {
                    return StatusCode(500, "Speech synthesis failed");
                }
            }
        }
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Pronunciation error");
        return StatusCode(500);
    }
}
```

---

## ?? Долгосрочные функции

### 8. Спaced Repetition System (SRS) для Flashcards

```csharp
// 1. Модель для flashcard
public class Flashcard
{
    public int Id { get; set; }
    public int WordId { get; set; }
    public int UserId { get; set; }
    public int Interval { get; set; } = 1;  // days
    public int EaseFactor { get; set; } = 2.5;  // 2.5x по умолчанию
    public DateTime NextReview { get; set; } = DateTime.UtcNow;
    public int Reviews { get; set; } = 0;
}

// 2. SRS Algorithm (SM-2)
public class SrsService
{
    public void UpdateFlashcard(Flashcard card, int quality)
    {
        // quality: 0-5 (0 = fail, 5 = perfect)

        if (quality < 3)
        {
            card.Interval = 1;
            card.EaseFactor = Math.Max(1.3, card.EaseFactor - 0.2);
        }
        else
        {
            if (card.Reviews == 0)
                card.Interval = 1;
            else if (card.Reviews == 1)
                card.Interval = 3;
            else
                card.Interval = (int)(card.Interval * card.EaseFactor);

            card.EaseFactor += 0.1 - (5 - quality) * (0.08 + (5 - quality) * 0.02);
            card.EaseFactor = Math.Max(1.3, card.EaseFactor);
        }

        card.Reviews++;
        card.NextReview = DateTime.UtcNow.AddDays(card.Interval);
    }
}

// 3. Endpoint для обучения
[HttpPost("flashcards/{cardId}/review")]
[Authorize]
public async Task<IActionResult> ReviewFlashcard(
    int cardId,
    [FromBody] ReviewRequest request)
{
    var card = await _context.Flashcards
        .FirstOrDefaultAsync(f => f.Id == cardId);

    if (card == null)
        return NotFound();

    _srsService.UpdateFlashcard(card, request.Quality);
    await _context.SaveChangesAsync();

    return Ok(new { nextReview = card.NextReview, interval = card.Interval });
}

// 4. Получить карточки для обучения
[HttpGet("flashcards/daily")]
[Authorize]
public async Task<IActionResult> GetDailyFlashcards()
{
    var userId = GetUserId();
    var now = DateTime.UtcNow;

    var flashcards = await _context.Flashcards
        .Include(f => f.Word)
        .Where(f => f.UserId == userId && f.NextReview <= now)
        .OrderBy(f => f.NextReview)
        .Take(20)  // 20 карточек в день
        .ToListAsync();

    return Ok(flashcards);
}
```

---

### 9. Лидерборд и сравнение статистики

```csharp
// 1. Статистика по ученикам
public class StudentStats
{
    public int UserId { get; set; }
    public string UserLogin { get; set; }
    public int TotalWordsLearned { get; set; }
    public double AverageAccuracy { get; set; }
    public int LongestStreak { get; set; }
    public DateTime LastActivityAt { get; set; }
}

// 2. Endpoint для лидерборда
[HttpGet("leaderboard")]
[Authorize]
public async Task<IActionResult> GetLeaderboard(
    [FromQuery] int classroomId,
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20)
{
    var userId = GetUserId();
    var classroom = await _context.Classrooms
        .FirstOrDefaultAsync(c => c.Id == classroomId && c.TeacherId == userId);

    if (classroom == null)
        return NotFound();

    var stats = await _context.Users
        .Where(u => u.UserId == userId)  // Students of this teacher
        .Select(u => new StudentStats
        {
            UserId = u.Id,
            UserLogin = u.Login,
            TotalWordsLearned = u.LearningProgresses
                .Where(lp => lp.Progress >= 5)
                .DistinctBy(lp => lp.WordId)
                .Count(),
            AverageAccuracy = u.LearningProgresses.Average(lp => lp.Progress),
            LastActivityAt = u.LearningProgresses
                .Max(lp => lp.LastReviewedAt) ?? DateTime.MinValue
        })
        .OrderByDescending(s => s.TotalWordsLearned)
        .ThenByDescending(s => s.AverageAccuracy)
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .ToListAsync();

    return Ok(stats);
}

// 3. Сравнение с одноклассниками
[HttpGet("compare/{studentId}")]
[Authorize]
public async Task<IActionResult> CompareWithStudent(int studentId)
{
    var userId = GetUserId();

    var myStats = await _context.Users
        .Where(u => u.Id == userId)
        .Select(u => new
        {
            u.Login,
            WordsLearned = u.LearningProgresses
                .Where(lp => lp.Progress >= 5)
                .DistinctBy(lp => lp.WordId)
                .Count(),
            AverageAccuracy = u.LearningProgresses.Average(lp => lp.Progress)
        })
        .FirstOrDefaultAsync();

    var theirStats = await _context.Users
        .Where(u => u.Id == studentId && u.UserId == userId)  // Must be my student
        .Select(u => new
        {
            u.Login,
            WordsLearned = u.LearningProgresses
                .Where(lp => lp.Progress >= 5)
                .DistinctBy(lp => lp.WordId)
                .Count(),
            AverageAccuracy = u.LearningProgresses.Average(lp => lp.Progress)
        })
        .FirstOrDefaultAsync();

    return Ok(new { myStats, theirStats });
}
```

---

### 10. Mobile API / React Native клиент

**Архитектура:**
```
CourseWork/
??? LearningAPI/              # Существующий API
??? LearningMobileApp/        # React Native / Flutter
?   ??? src/
?   ?   ??? screens/
?   ?   ??? components/
?   ?   ??? services/
?   ?   ??? navigation/
?   ??? app.json
??? ...
```

**Начальная структура React Native:**
```bash
npx react-native init LearningMobileApp
cd LearningMobileApp
npm install axios @react-navigation/native @react-navigation/bottom-tabs
```

**API сервис:**
```javascript
// services/api.js
import axios from 'axios';
import AsyncStorage from '@react-native-async-storage/async-storage';

const api = axios.create({
    baseURL: 'http://localhost:5077/api'
});

api.interceptors.request.use(async (config) => {
    const token = await AsyncStorage.getItem('access_token');
    if (token) {
        config.headers.Authorization = `Bearer ${token}`;
    }
    return config;
});

api.interceptors.response.use(
    response => response,
    async error => {
        const originalRequest = error.config;
        if (error.response?.status === 401 && !originalRequest._retry) {
            originalRequest._retry = true;

            const refreshToken = await AsyncStorage.getItem('refresh_token');
            try {
                const response = await axios.post(
                    'http://localhost:5077/api/auth/refresh',
                    { refreshToken }
                );
                await AsyncStorage.setItem('access_token', response.data.accessToken);
                return api(originalRequest);
            } catch (err) {
                await AsyncStorage.removeItem('access_token');
            }
        }
        return Promise.reject(error);
    }
);

export const authService = {
    login: (username, password) =>
        api.post('/auth/login', { username, password }),
    register: (data) =>
        api.post('/auth/register', data),
};

export const dictionaryService = {
    getDictionaries: () =>
        api.get('/dictionaries'),
    getDictionary: (id) =>
        api.get(`/dictionaries/${id}`),
    createDictionary: (data) =>
        api.post('/dictionaries', data),
};

export default api;
```

**Screen компонент:**
```javascript
// screens/DictionariesScreen.js
import React, { useEffect, useState } from 'react';
import { View, FlatList, TouchableOpacity, Text, StyleSheet } from 'react-native';
import { dictionaryService } from '../services/api';

export default function DictionariesScreen({ navigation }) {
    const [dictionaries, setDictionaries] = useState([]);
    const [loading, setLoading] = useState(true);

    useEffect(() => {
        loadDictionaries();
    }, []);

    const loadDictionaries = async () => {
        try {
            const response = await dictionaryService.getDictionaries();
            setDictionaries(response.data);
        } catch (error) {
            console.error(error);
        } finally {
            setLoading(false);
        }
    };

    return (
        <View style={styles.container}>
            <FlatList
                data={dictionaries}
                keyExtractor={(item) => item.id.toString()}
                renderItem={({ item }) => (
                    <TouchableOpacity
                        style={styles.item}
                        onPress={() =>
                            navigation.navigate('Dictionary', { id: item.id })
                        }
                    >
                        <Text style={styles.title}>{item.name}</Text>
                        <Text style={styles.description}>
                            {item.words.length} слов
                        </Text>
                    </TouchableOpacity>
                )}
                refreshing={loading}
                onRefresh={loadDictionaries}
            />
        </View>
    );
}

const styles = StyleSheet.create({
    container: {
        flex: 1,
        paddingTop: 20,
    },
    item: {
        padding: 20,
        borderBottomWidth: 1,
        borderBottomColor: '#ddd',
    },
    title: {
        fontSize: 18,
        fontWeight: 'bold',
    },
    description: {
        fontSize: 14,
        color: '#666',
        marginTop: 5,
    },
});
```

---

## ??? Архитектурные улучшения

### Добавить Specification Pattern

```csharp
// LearningTrainerShared\Specifications\Specification.cs
public abstract class Specification<T> where T : class
{
    public IQueryable<T> Query { get; set; }

    protected virtual void AddInclude(Expression<Func<T, object>> include)
    {
        Query = Query.Include(include);
    }

    protected virtual void AddIncludeString(string include)
    {
        Query = Query.Include(include);
    }

    protected virtual void ApplyPaging(int skip, int take)
    {
        Query = Query.Skip(skip).Take(take);
    }

    protected virtual void ApplyOrdering(
        Expression<Func<T, object>> orderBy, bool descending = false)
    {
        Query = descending
            ? Query.OrderByDescending(orderBy)
            : Query.OrderBy(orderBy);
    }
}

// Пример использования
public class UserWithDictionariesSpecification : Specification<User>
{
    public UserWithDictionariesSpecification(int userId)
    {
        Query = new();
        Query = Query.Where(u => u.Id == userId);
        AddInclude(u => u.Dictionaries);
        AddIncludeString("Dictionaries.Words");
    }
}

// В контроллере
var spec = new UserWithDictionariesSpecification(userId);
var user = await _context.Users.WithSpecification(spec).FirstOrDefaultAsync();
```

### Использовать Repository Pattern

```csharp
public interface IRepository<T> where T : class
{
    Task<T> GetByIdAsync(int id);
    Task<List<T>> GetAllAsync();
    Task<T> AddAsync(T entity);
    Task UpdateAsync(T entity);
    Task DeleteAsync(T entity);
}

public class Repository<T> : IRepository<T> where T : class
{
    private readonly ApiDbContext _context;
    private readonly DbSet<T> _dbSet;

    public Repository(ApiDbContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }

    public async Task<T> GetByIdAsync(int id)
        => await _dbSet.FindAsync(id);

    public async Task<List<T>> GetAllAsync()
        => await _dbSet.ToListAsync();

    public async Task<T> AddAsync(T entity)
    {
        _dbSet.Add(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task UpdateAsync(T entity)
    {
        _dbSet.Update(entity);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(T entity)
    {
        _dbSet.Remove(entity);
        await _context.SaveChangesAsync();
    }
}
```

### Unit of Work Pattern

```csharp
public interface IUnitOfWork : IDisposable
{
    IRepository<Dictionary> DictionaryRepository { get; }
    IRepository<Word> WordRepository { get; }
    IRepository<LearningProgress> ProgressRepository { get; }

    Task CommitAsync();
    Task RollbackAsync();
}

public class UnitOfWork : IUnitOfWork
{
    private readonly ApiDbContext _context;

    public IRepository<Dictionary> DictionaryRepository { get; }
    public IRepository<Word> WordRepository { get; }
    public IRepository<LearningProgress> ProgressRepository { get; }

    public UnitOfWork(ApiDbContext context)
    {
        _context = context;
        DictionaryRepository = new Repository<Dictionary>(_context);
        WordRepository = new Repository<Word>(_context);
        ProgressRepository = new Repository<LearningProgress>(_context);
    }

    public async Task CommitAsync()
        => await _context.SaveChangesAsync();

    public async Task RollbackAsync()
        => await _context.Database.RollbackTransactionAsync();

    public void Dispose()
        => _context.Dispose();
}
```

---

## ?? Итоговый приоритет реализации

1. ? **RBAC + Refresh Token** (2 дня) — критично для безопасности
2. ? **Audit Logging** (1 день) — соответствие требованиям
3. ? **Offline Sync** (3-4 дня) — улучшает UX
4. ? **WebSocket/SignalR** (2-3 дня) — real-time функции
5. ? **Export/Import** (1-2 дня) — удобство
6. ?? **TTS/Pronunciation** (1 день) — nice-to-have
7. ?? **SRS/Flashcards** (5-6 дней) — долгосрочный проект
8. ?? **Leaderboard** (1-2 дня) — gamification
9. ?? **Mobile App** (2-3 недели) — расширение платформ

