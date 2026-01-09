# ?? Техническое резюме проекта LearningTrainer

## ?? Быстрый обзор

| Аспект | Описание |
|--------|---------|
| **Название** | LearningTrainer (CourseWork) |
| **Тип** | Система управления обучением (LMS) для иностранных языков |
| **Платформы** | Windows Desktop (WPF) + Web API (REST) |
| **Язык** | C# 12 / .NET 8 |
| **Архитектура** | N-tier + MVVM + CQRS |
| **БД** | SQL Server (API) + SQLite (Client) + Redis (Cache) |
| **Статус** | Активная разработка ?? |

---

## ?? Статистика проекта

### Размер кодовой базы

| Проект | Файлов | Строк кода | Назначение |
|--------|--------|-----------|-----------|
| **LearningTrainer** | 65+ | ~15 000 | WPF Desktop Client |
| **LearningAPI** | 10+ | ~2 500 | REST API Backend |
| **LearningTrainerShared** | 40+ | ~5 000 | Shared Models & Logic |
| **StressTestClient** | 1 | ~200 | Load Testing |
| **TOTAL** | 116+ | ~22 700 | Всё вместе |

### Распределение компонентов

```
Frontend (WPF)
??? Views & ViewModels      30 файлов (~7000 строк)
??? Services               15 файлов (~4000 строк)
??? Core & Utilities       15 файлов (~2500 строк)
??? Converters & Behaviors  5 файлов (~500 строк)

Backend (API)
??? Controllers             7 файлов (~1500 строк)
??? Services               2 файлов (~300 строк)
??? Configuration          1 файл (~150 строк)

Shared
??? Entities               8 файлов (~600 строк)
??? DTOs & Requests       8 файлов (~400 строк)
??? DbContexts            3 файла (~400 строк)
??? MediatR Handlers      2 файла (~300 строк)
??? Migrations            7 файлов (~800 строк)
```

---

## ?? Технологический стек - детально

### Backend (.NET 8)

```
ASP.NET Core Web API
??? Authentication & Authorization
?   ??? JWT Bearer (Microsoft.AspNetCore.Authentication.JwtBearer)
?   ??? Role-based Access Control (RBAC)
??? Data Access
?   ??? Entity Framework Core 9.0.10
?   ?   ??? SQL Server (Microsoft.EntityFrameworkCore.SqlServer)
?   ?   ??? SQLite (Microsoft.Data.Sqlite)
?   ?   ??? Migrations (20251126...)
?   ??? Connection Pooling
??? Business Logic
?   ??? MediatR 14.0.0 (CQRS Pattern)
?   ??? Dependency Injection (Built-in)
??? Caching
?   ??? Redis (Microsoft.Extensions.Caching.StackExchangeRedis)
??? API Documentation
?   ??? Swagger/OpenAPI (Swashbuckle.AspNetCore 6.6.2)
??? Security
?   ??? BCrypt.Net (4.0.3) - Password Hashing
?   ??? HTTPS/TLS
??? Utilities
?   ??? Nanoid (3.1.0) - Unique ID Generation
?   ??? System.Net.Http.Json
??? Tools
    ??? Visual Studio Code Generation (8.0.7)
```

### Frontend (WPF)

```
Windows Presentation Foundation
??? UI Framework
?   ??? XAML-based UI
?   ??? Data Binding
?   ??? Attached Behaviors
??? Architecture
?   ??? MVVM Pattern
?   ?   ??? ViewModels (~15)
?   ?   ??? Views (~12)
?   ?   ??? Converters (~6)
?   ??? Services (~12)
??? Content Rendering
?   ??? Markdown (Markdig 0.43.0 + MdXaml 1.27.0)
?   ??? SVG (SharpVectors.Wpf 1.8.5)
?   ??? WebView2 (1.0.3595.46)
??? Data Access
?   ??? EF Core + SQLite
?   ??? HTTP Client for API
?   ??? Local Database
??? Features
?   ??? Fuzzy Search (FuzzySharp 2.0.2)
?   ??? Spell Check
?   ??? Theme Management
?   ??? Localization (i18n)
??? Storage
    ??? LocalData Service
    ??? Session Persistence
    ??? Settings Management
```

### Базы данных

```
SQL Server (Production API)
??? Users
?   ??? Id, Login, PasswordHash, RoleId
?   ??? Teacher-Student relationship (UserId FK)
?   ??? Invite Codes
?   ??? Refresh Tokens (в разработке)
??? Dictionaries
?   ??? Id, UserId, Name, Description
?   ??? LanguageFrom, LanguageTo
?   ??? Soft Delete
??? Words
?   ??? Id, DictionaryId
?   ??? OriginalWord, Translation, Example
?   ??? Difficulty Level
??? Rules
?   ??? Id, UserId, Content (Markdown)
?   ??? Categories
??? Learning Progress
?   ??? UserId, WordId (Composite Key)
?   ??? Progress (0-10), Interval
?   ??? NextReview, LastReviewedAt
?   ??? Indexed for queries
??? Sharing
?   ??? DictionarySharing (User-Dictionary)
?   ??? RuleSharing (User-Rule)
??? Roles
    ??? Admin, Teacher, Student
    ??? Permissions (в разработке)

SQLite (Local Client)
??? Зеркало SQL Server
??? Offline Support
??? Local Synchronization Log
??? Settings & Preferences
```

---

## ?? API Endpoints (REST)

### Authentication
```
POST   /api/auth/login               - Login
POST   /api/auth/register            - Register
POST   /api/auth/refresh             - Refresh Token (TODO)
POST   /api/auth/logout              - Logout
PUT    /api/auth/change-password     - Change Password
```

### Dictionaries
```
GET    /api/dictionaries             - Get all user's dictionaries (with pagination)
GET    /api/dictionaries/{id}        - Get dictionary with words
POST   /api/dictionaries             - Create new dictionary
PUT    /api/dictionaries/{id}        - Update dictionary
DELETE /api/dictionaries/{id}        - Delete dictionary
GET    /api/dictionaries/list/available  - Get dictionaries shared to user
GET    /api/dictionaries/{id}/export/csv - Export to CSV
POST   /api/dictionaries/import/csv  - Import from CSV
```

### Words
```
GET    /api/words/dictionary/{id}    - Get words by dictionary
POST   /api/words                    - Add word
PUT    /api/words/{id}               - Update word
DELETE /api/words/{id}               - Delete word
GET    /api/words/{id}/pronounce     - Get pronunciation
```

### Rules (Grammar)
```
GET    /api/rules                    - Get all user's rules
GET    /api/rules/{id}               - Get rule details
POST   /api/rules                    - Create rule
PUT    /api/rules/{id}               - Update rule
DELETE /api/rules/{id}               - Delete rule
```

### Learning & Progress
```
GET    /api/progress/dashboard       - Get learning statistics
POST   /api/progress/update          - Update word progress
GET    /api/progress/word/{id}       - Get word progress
POST   /api/flashcards/daily         - Get cards for today
POST   /api/flashcards/{id}/review   - Review flashcard
GET    /api/leaderboard              - Get leaderboard
```

### Sharing
```
GET    /api/sharing/dictionary/{id}/status  - Get shared users list
POST   /api/sharing/dictionary/toggle       - Toggle sharing
POST   /api/sharing/rules/toggle            - Toggle rule sharing
```

### Classroom (Teachers only)
```
GET    /api/classroom                       - Get my classroom
POST   /api/classroom/students/add          - Add student
POST   /api/classroom/students/{id}/remove  - Remove student
```

---

## ??? Архитектурные паттерны

### 1. MVVM (Model-View-ViewModel)
**Где:** WPF клиент
**Компоненты:**
- **Model:** Entities из LearningTrainerShared
- **View:** XAML файлы
- **ViewModel:** Логика представления, команды, свойства

```csharp
// Пример
public class DictionaryViewModel : TabViewModelBase
{
    private Dictionary _selectedDictionary;
    public Dictionary SelectedDictionary
    {
        get => _selectedDictionary;
        set { SetProperty(ref _selectedDictionary, value); }
    }

    public RelayCommand LoadDictionariesCommand { get; }
    
    public DictionaryViewModel()
    {
        LoadDictionariesCommand = new RelayCommand(async () =>
            await LoadDictionaries());
    }
}
```

### 2. CQRS (Command Query Responsibility Segregation)
**Где:** LearningAPI + MediatR
**Паттерн:**
- **Queries:** Только чтение (GetDictionariesQuery)
- **Commands:** Изменение состояния (CreateDictionaryCommand)
- **Handlers:** Реализация логики

```csharp
// Query
public class GetDictionariesQuery : IRequest<List<Dictionary>>
{
    public int UserId { get; set; }
}

// Handler
public class GetDictionariesHandler 
    : IRequestHandler<GetDictionariesQuery, List<Dictionary>>
{
    private readonly ApiDbContext _context;

    public async Task<List<Dictionary>> Handle(
        GetDictionariesQuery request, CancellationToken ct)
    {
        return await _context.Dictionaries
            .Where(d => d.UserId == request.UserId)
            .Include(d => d.Words)
            .ToListAsync(ct);
    }
}

// Usage в контроллере
var dictionaries = await _mediator.Send(new GetDictionariesQuery { UserId = userId });
```

### 3. Repository Pattern
**Где:** LocalDataService, ApiDataService
**Цель:** Абстракция над источником данных

```csharp
public interface IDataService
{
    Task<List<Dictionary>> GetDictionariesAsync();
    Task<Dictionary> AddDictionaryAsync(Dictionary dictionary);
    Task<bool> DeleteDictionaryAsync(int dictionaryId);
}

// Implementation 1: Local (SQLite)
public class LocalDataService : IDataService { }

// Implementation 2: Remote (REST API)
public class ApiDataService : IDataService { }
```

### 4. Dependency Injection
**Где:** Везде
**Контейнер:** Microsoft.Extensions.DependencyInjection

```csharp
// API Startup
builder.Services.AddScoped<ApiDbContext>();
builder.Services.AddScoped<TokenService>();
builder.Services.AddHttpClient<ExternalDictionaryService>();
builder.Services.AddMediatR(cfg => 
    cfg.RegisterServicesFromAssembly(typeof(GetDictionariesHandler).Assembly));

// WPF (Manual)
var context = new ApiDbContext();
var tokenService = new TokenService();
var apiService = new ApiDataService();
```

### 5. Observer Pattern (Event Aggregator)
**Где:** WPF клиент
**Цель:** Loose coupling между ViewModels

```csharp
public class EventAggregator
{
    private static Dictionary<string, Action<object>> _subscribers = new();

    public static void Subscribe(string eventType, Action<object> action)
        => _subscribers.Add(eventType, action);

    public static void Publish(string eventType, object data)
        => _subscribers[eventType]?.Invoke(data);
}

// Использование
EventAggregator.Subscribe("DictionaryCreated", (data) =>
{
    LoadDictionaries();  // Refresh
});

EventAggregator.Publish("DictionaryCreated", newDictionary);
```

---

## ?? Безопасность

### Authentication
- ? JWT Bearer tokens
- ? BCrypt пароли
- ? Token validation on every API call
- ? Refresh tokens (в разработке)
- ? 2FA (not implemented)

### Authorization
- ? Role-based (Admin, Teacher, Student)
- ? Resource ownership checks
- ? Fine-grained permissions (в разработке)
- ? Audit logging (в разработке)

### Data Protection
- ? Parameterized queries (EF Core)
- ? HTTPS/TLS (в production)
- ? Data encryption at rest
- ? Secrets management (appsettings.json на диске)

### Vulnerabilities (Известные проблемы)
- ? No input validation (FluentValidation needed)
- ? No rate limiting
- ? No CORS properly configured
- ? Information leakage in error messages
- ? No CSRF protection (API)

---

## ?? Миграции базы данных

```
20251123180916_InitialDatabase
??? Users table
??? Dictionaries table
??? Words table
??? Rules table
??? Roles table
??? Foreign keys

20251126182146_DBUpdate
??? DictionarySharing table
??? RuleSharing table
??? Relationships update

20251126183418_DBUpdate2
??? Additional fields/indexes
```

**Статус:** ?? Inconsistent - 3 миграции близко к дате создания, что указывает на проблемы со схемой.

---

## ?? Тестирование

### Текущее состояние
- ? Нет unit тестов
- ? Нет integration тестов
- ? Нет E2E тестов
- ? StressTestClient для нагрузочного тестирования (базовый)

### Рекомендуемые фреймворки
```bash
# Unit Testing
dotnet add package xunit
dotnet add package xunit.runner.visualstudio
dotnet add package Moq
dotnet add package FluentAssertions

# Integration Testing
dotnet add package Microsoft.AspNetCore.Mvc.Testing

# API Testing
dotnet add package RestSharp
```

### Пример unit теста
```csharp
[TestClass]
public class TokenServiceTests
{
    private readonly TokenService _tokenService;
    private readonly IConfiguration _config;

    [TestInitialize]
    public void Setup()
    {
        _config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                { "Jwt:Key", "your-secret-key-here" },
                { "Jwt:Issuer", "LearningTrainer" },
                { "Jwt:Audience", "LearningTrainerUsers" }
            })
            .Build();

        _tokenService = new TokenService(_config);
    }

    [TestMethod]
    public void GenerateAccessToken_WithValidUser_ReturnsValidToken()
    {
        // Arrange
        var user = new User { Id = 1, Login = "test@example.com" };

        // Act
        var token = _tokenService.GenerateAccessToken(user);

        // Assert
        Assert.IsNotNull(token);
        Assert.IsTrue(token.Split('.').Length == 3);  // JWT format
    }
}
```

---

## ?? Зависимости и версии

### Critical Dependencies
```
Entity Framework Core 9.0.10       ? Latest stable
ASP.NET Core 8.0                   ? Long-term support
MediatR 14.0.0                     ? Latest
JWT Bearer 8.0.21                  ? Latest
```

### Minor Dependencies
```
BCrypt.Net-Next 4.0.3              ? Current
Nanoid 3.1.0                       ? Current
Markdig 0.43.0                     ? Current
Redis Cache 10.0.1                 ? Latest
Swagger 6.6.2                      ? Latest
```

### Potential Issues
- ?? Microsoft.Web.WebView2 (1.0.3595.46) может иметь обновления
- ?? SharpVectors.Wpf (1.8.5) мало обновляется
- ?? MdXaml (1.27.0) - старый пакет

---

## ?? Deployment

### Current Setup
- ? No Docker
- ? No CI/CD pipeline
- ? No automated testing
- ? No monitoring/logging infrastructure
- ? Manual database migrations

### Production Checklist
- [ ] Docker образы для API
- [ ] GitHub Actions для CI/CD
- [ ] SSL/TLS сертификаты
- [ ] Database backups
- [ ] Monitoring (Application Insights)
- [ ] Logging (Serilog to centralized service)
- [ ] Load balancing
- [ ] Rate limiting
- [ ] API versioning

---

## ?? Performance Metrics

### Current State
| Метрика | Статус | Проблема |
|---------|--------|---------|
| Query Performance | ?? | Нет оптимизации, N+1 проблемы |
| Caching | ?? | Redis настроен, но не используется |
| Pagination | ? | Нет пагинации в списках |
| API Response Time | ? | Не измеряется |
| Database Indexes | ?? | Только на NextReview |
| Connection Pooling | ? | EF Core default |

### Optimization Opportunities
1. Добавить индексы на часто фильтруемые поля
2. Использовать Redis для кеширования словарей
3. Реализовать пагинацию
4. Добавить Include() для N+1 предотвращения
5. Использовать AsNoTracking() для чтения

---

## ?? Maturity Assessment

| Категория | Score | Status |
|-----------|-------|--------|
| **Architecture** | 7/10 | Solid design, good patterns |
| **Code Quality** | 6/10 | Clean, but needs tests |
| **Security** | 5/10 | Basic auth, needs hardening |
| **Documentation** | 4/10 | Comments minimal |
| **Testing** | 2/10 | No tests |
| **DevOps** | 2/10 | Manual everything |
| **Performance** | 6/10 | Decent, some issues |
| **Scalability** | 5/10 | Single server ready |

**Overall:** 5.1/10 - Good for learning project, needs production hardening

---

## ??? Development Roadmap

### Phase 1 (1-2 weeks) - Stabilization
- [ ] Fix critical security issues
- [ ] Add input validation
- [ ] Implement unit tests (30% coverage)
- [ ] Add logging

### Phase 2 (2-3 weeks) - Features
- [ ] RBAC system
- [ ] Refresh tokens
- [ ] Offline sync
- [ ] Audit logging

### Phase 3 (1 month) - Enhancement
- [ ] WebSocket/SignalR
- [ ] Export/Import
- [ ] Flashcards + SRS
- [ ] Mobile API

### Phase 4 (2+ months) - Scale
- [ ] Docker + Kubernetes
- [ ] Distributed caching
- [ ] Microservices (optional)
- [ ] Mobile apps (iOS/Android)

---

## ?? Документация файлов

| Файл | Назначение |
|------|-----------|
| **PROJECT_ANALYSIS.md** | Полный анализ технологий, проблем, рекомендаций |
| **FIXING_GUIDE.md** | Пошаговое исправление критических ошибок |
| **FEATURES_RECOMMENDATIONS.md** | Детальное руководство по новым функциям |
| **TECHNICAL_SUMMARY.md** | Этот файл - быстрый справочник |

---

## ?? Полезные ссылки

- [GitHub репо](https://github.com/Sugoman/CourseWork)
- [Entity Framework Core Docs](https://docs.microsoft.com/en-us/ef/core/)
- [MediatR Pattern](https://jasonwatmore.com/post/2022/03/21/net-6-minimal-api-with-mediatr)
- [MVVM in WPF](https://docs.microsoft.com/en-us/archive/msdn-magazine/2009/february/patterns-mvvm)
- [REST API Best Practices](https://restfulapi.net/)
- [ASP.NET Core Security](https://docs.microsoft.com/en-us/aspnet/core/security/)

---

## ????? Контакты разработчика

Repository: https://github.com/Sugoman/CourseWork

Последнее обновление: $(date)

