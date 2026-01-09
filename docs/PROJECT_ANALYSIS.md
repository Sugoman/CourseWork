# Анализ проекта LearningTrainer (CourseWork)

## ?? Содержание
1. [Обзор проекта](#обзор-проекта)
2. [Архитектура решения](#архитектура-решения)
3. [Используемые технологии](#используемые-технологии)
4. [Структура проекта](#структура-проекта)
5. [Возможные ошибки и недочёты](#возможные-ошибки-и-недочёты)
6. [Рекомендации по улучшению](#рекомендации-по-улучшению)

---

## ?? Обзор проекта

**LearningTrainer** — это многоуровневое приложение для обучения иностранным языкам с использованием словарей и правил грамматики. Система состоит из:
- **WPF клиент** (Windows Desktop) для преподавателей и студентов
- **REST API** для управления данными и обучением
- **Общая бизнес-логика** в виде shared библиотеки

**Целевая аудитория:** Преподаватели, которые создают словари и правила, и студенты, которые учатся.

---

## ??? Архитектура решения

### Многослойная архитектура

```
???????????????????????????????????????????????
?         WPF Client (LearningTrainer)        ?
?  Views, ViewModels, Services, Converters   ?
???????????????????????????????????????????????
               ? HTTP (REST API)
???????????????????????????????????????????????
?         API Layer (LearningAPI)             ?
?  Controllers, Services, Authentication     ?
???????????????????????????????????????????????
               ? EF Core DbContext
???????????????????????????????????????????????
?    Data Access & Business Logic             ?
?  (LearningTrainerShared)                   ?
?  - Models/Entities                         ?
?  - DbContexts (Local + API)                ?
?  - MediatR Handlers (CQRS)                 ?
?  - DTOs & Requests                         ?
???????????????????????????????????????????????
               ?
???????????????????????????????????????????????
?    Data Storage                             ?
?  - SQL Server (API)                        ?
?  - SQLite (Client)                         ?
?  - Redis (Cache)                           ?
?????????????????????????????????????????????????
```

### Паттерны проектирования

1. **MVVM** (Model-View-ViewModel) — WPF клиент
2. **Repository/Service Pattern** — DataService, ApiDataService
3. **MediatR CQRS** — GetDictionariesQuery/Handler
4. **Dependency Injection** — встроенный ASP.NET Core DI
5. **JWT Authentication** — защита API

---

## ??? Используемые технологии

### Технологический стек

| Компонент | Версия | Назначение |
|-----------|--------|-----------|
| **.NET** | 8.0 | Основная платформа (Windows desktop + REST API) |
| **WPF** | 8.0-windows | GUI клиентской части |
| **Entity Framework Core** | 9.0.10 | ORM для доступа к БД |
| **SQL Server** | - | БД для API |
| **SQLite** | 9.0.9 | Локальная БД для клиента |
| **MediatR** | 14.0.0 | CQRS/Mediator паттерн |
| **BCrypt.Net-Next** | 4.0.3 | Хеширование паролей |
| **JWT Bearer** | 8.0.21 | Аутентификация API |
| **Redis** | 10.0.1 | Распределённое кеширование |
| **Swagger/Swashbuckle** | 6.6.2 | Документация API |
| **Nanoid** | 3.1.0 | Генерация уникальных кодов приглашения |
| **Markdig** | 0.43.0 | Парсинг Markdown для правил |
| **MdXaml** | 1.27.0 | Отрисовка Markdown в WPF |
| **FuzzySharp** | 2.0.2 | Нечёткий поиск слов |
| **SharpVectors.Wpf** | 1.8.5 | Отрисовка SVG в WPF |

### Фреймворки и платформы

- **ASP.NET Core** (WebApi) — RESTful API
- **WPF** — Десктопное приложение
- **Entity Framework Core** — ORM
- **MediatR** — CQRS

---

## ?? Структура проекта

### 1. **LearningTrainer** (WPF Client)

```
LearningTrainer/
??? Views/                    # XAML представления
?   ??? LoginView.xaml
?   ??? DashboardView.xaml
?   ??? LearningView.xaml
?   ??? DictionaryManagementView.xaml
?   ??? ShareDictionaryView.xaml
?   ??? SettingsView.xaml
?   ??? ...
??? ViewModels/              # MVVM ViewModels
?   ??? LoginViewModel.cs
?   ??? MainViewModel.cs
?   ??? DashboardViewModel.cs
?   ??? LearningViewModel.cs
?   ??? ShareContentViewModel.cs
?   ??? ...
??? Services/                # Бизнес-логика
?   ??? ApiDataService.cs    # HTTP клиент для API
?   ??? LocalDataService.cs  # Локальные данные
?   ??? TokenService.cs      # JWT токены
?   ??? SessionService.cs    # Сессии пользователей
?   ??? SettingsService.cs   # Настройки приложения
?   ??? SpellCheckService.cs # Проверка орфографии
?   ??? ExternalDictionaryService.cs # Внешние словари
?   ??? DialogService.cs     # Диалоги
??? Core/                    # Инфраструктура
?   ??? RelayCommand.cs      # ICommand реализация
?   ??? ObservableObject.cs  # Базовый класс ViewModel
?   ??? EventAggregator.cs   # Event Bus
?   ??? ThemeService.cs      # Темизация
?   ??? LocalizationManager.cs # i18n
?   ??? ...
??? Behaviors/               # Attached Behaviors
?   ??? PasswordBoxBehavior.cs
?   ??? WebBrowserBehavior.cs
??? Converters/              # XAML Value Converters
?   ??? BooleanToVisibilityConverter.cs
?   ??? InvertedBooleanConverter.cs
?   ??? ...
??? Migrations/              # EF Core migrations (SQLite)
??? App.xaml.cs             # Entry point
```

**Ключевые компоненты:**
- **MVVM паттерн** — разделение логики и UI
- **ApiDataService** — HTTP клиент для взаимодействия с API
- **LocalDataService** — работа с локальной БД
- **SessionService** — сохранение состояния сессии

### 2. **LearningAPI** (REST API)

```
LearningAPI/
??? Controllers/             # API endpoints
?   ??? AuthController.cs    # Аутентификация (login, register)
?   ??? DictionaryController.cs  # Управление словарями
?   ??? WordController.cs    # Управление словами
?   ??? RuleController.cs    # Управление правилами
?   ??? SharingController.cs # Распределение контента
?   ??? ProgressController.cs # Отслеживание прогресса
?   ??? TestController.cs    # Тесты (?)
?   ??? ClassroomController.cs # Управление классами
??? Services/
?   ??? TokenService.cs      # JWT генерация
?   ??? ExternalDictionaryService.cs
??? APIProgram.cs            # Конфигурация Startup
```

**Ключевые endpoint'ы:**
- `POST /api/auth/login` — Вход в систему
- `POST /api/auth/register` — Регистрация
- `GET /api/dictionaries` — Получить словари
- `POST /api/dictionaries` — Создать словарь
- `GET /api/dictionaries/{id}` — Получить словарь по ID
- `POST /api/words` — Добавить слово
- `POST /api/sharing/dictionary/toggle` — Поделиться словарём
- `GET /api/progress/dashboard` — Статистика обучения

### 3. **LearningTrainerShared** (Shared Library)

```
LearningTrainerShared/
??? Models/
?   ??? Entities/             # EF Core entities
?   ?   ??? User.cs          # Пользователь (Teacher, Student, Admin)
?   ?   ??? Dictionary.cs    # Словарь
?   ?   ??? Word.cs          # Слово
?   ?   ??? Rule.cs          # Правило грамматики
?   ?   ??? LearningProgress.cs # Прогресс обучения
?   ?   ??? Role.cs          # Роль (Teacher, Student, Admin)
?   ?   ??? DictionarySharing.cs # Распределение словарей
?   ?   ??? RuleSharing.cs   # Распределение правил
?   ?   ??? UserRelationship.cs # Отношения (Teacher-Student)
?   ??? Features/            # DTOs и бизнес-модели
?       ??? Auth/
?       ?   ??? RegisterRequest.cs
?       ?   ??? LoginRequest.cs
?       ?   ??? UserSessionDto.cs
?       ??? Dictionaries/
?       ?   ??? CreateDictionaryRequest.cs
?       ?   ??? CreateWordRequest.cs
?       ?   ??? DictionaryApiEntryDto.cs
?       ??? Learning/
?       ?   ??? UpdateProgressRequest.cs
?       ?   ??? DashboardStats.cs
?       ?   ??? UpgradeResultDto.cs
?       ??? Rules/
?           ??? RuleCreateDto.cs
??? Context/                 # DbContexts
?   ??? ApiDbContext.cs      # SQL Server context (API)
?   ??? LocalDbContext.cs    # SQLite context (Client)
?   ??? LearningAppDbContextFactory.cs
??? Migrations/              # EF Core миграции
```

### 4. **StressTestClient** (Load Testing)

```
StressTestClient/
??? Program.cs              # Простой инструмент для нагрузочного тестирования API
```

---

## ?? Возможные ошибки и недочёты

### ?? Критические ошибки

#### 1. **SQL Injection в SharingController** 
```csharp
// ? ПРОБЛЕМА в SharingController.cs, метод ToggleDictionarySharing
var student = await _context.Users
    .FirstOrDefaultAsync(u => u.Id == request.StudentId && u.UserId == teacherId);
```
**Проблема:** Логика неправильна. `u.UserId == teacherId` проверяет, является ли студент учеником конкретного учителя. Но студент может быть учеником без явной связи.

#### 2. **Утечка информации в Forbid ответах**
```csharp
// ? В множеством контроллеров
return Forbid("Словарь не найден или не принадлежит вам.");
```
**Проблема:** Сообщение раскрывает, что словарь существует. Лучше возвращать genericly: "Access denied".

#### 3. **Отсутствие валидации входных данных**
```csharp
// ? В контроллерах
[HttpPost("update")]
public async Task<IActionResult> UpdateProgress([FromBody] UpdateProgressRequest request)
{
    // Нет проверки null, пустых значений
    var wordExists = await _context.Words.AnyAsync(w => w.Id == request.WordId);
```

#### 4. **Race Condition в DictionarySharing**
```csharp
// ? Two-step операция без транзакции
var sharingEntry = await _context.DictionarySharings
    .FirstOrDefaultAsync(...);
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
**Проблема:** Между проверкой и добавлением может быть конфликт. Нужна UPSERT или транзакция.

#### 5. **Жёсткий BASE URL в ApiDataService**
```csharp
// ? LearningTrainer\Services\ApiDataService.cs
_httpClient = new HttpClient
{
    BaseAddress = new Uri("http://localhost:5077")  // ?? Hardcoded!
};
```
**Проблема:** URL localhost:5077 жестко закодирован. Нужна конфигурация.

#### 6. **Отсутствие CORS конфигурации**
```csharp
// ? APIProgram.cs
app.UseCors();  // Пусто! Потенциальный CORS проблемы
```

#### 7. **Нет обработки исключений в контроллерах**
```csharp
// ? Нет try-catch
public async Task<IActionResult> GetDictionaries()
{
    var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    if (!int.TryParse(userIdString, out var userId)) return Unauthorized();
    // ... если API упадёт, будет 500 без нормального сообщения
}
```

#### 8. **Потенциальный Null Reference в GetUserId**
```csharp
// ? SharingController.cs
private int GetUserId() => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
```
**Проблема:** Если `ClaimTypes.NameIdentifier` не установлен, будет `NullReferenceException`.

#### 9. **Отсутствие сортировки и пагинации**
```csharp
// ? GetDictionaries может вернуть неограниченное количество
public async Task<List<Dictionary>> GetDictionaries()
{
    // Нет Skip/Take/OrderBy
}
```

#### 10. **Логирование отсутствует**
Нет `ILogger` в контроллерах для отслеживания ошибок в production.

### ?? Серьёзные недочёты

#### 1. **Нет DTO для Dictionary в API ответах**
```csharp
// Возвращает сущность с всеми полями, включая чувствительные
return Ok(dictionary);
```
**Лучше:** Использовать DictionaryDto с ограниченными полями.

#### 2. **Отсутствие оптимизма в EF запросах**
```csharp
// ? N+1 проблема возможна
var dictionaries = await _context.Dictionaries.ToListAsync();
foreach(var dict in dictionaries) {
    var words = dict.Words;  // Lazy loading может вызвать доп. запросы
}
```
**Лучше:** Использовать `.Include(d => d.Words)`.

#### 3. **Дублирование TokenService**
- `LearningAPI\Services\TokenService.cs`
- `LearningTrainer\Services\TokenService.cs`

Должна быть одна реализация в `LearningTrainerShared`.

#### 4. **Роли захардкодированы в строках**
```csharp
// "Admin", "Teacher", "Student" разбросаны по коду
```
**Лучше:** Enum или constants.

#### 5. **Отсутствие валидации User-Teacher связи**
```csharp
// Что если teacher = null?
var student = await _context.Users
    .FirstOrDefaultAsync(u => u.Id == request.StudentId && u.UserId == teacherId);
```

#### 6. **SharedViewModel и StudentSharingViewModel** — дублирование?
Возможно, эти two классы делают похожее.

#### 7. **Нет обработки конкурентного доступа**
Несколько пользователей могут редактировать одну Dictionary одновременно ? конфликты.

#### 8. **Миграции базы данных не версионированы**
Есть 3 миграции (`20251123...`, `20251126...`, `20251126...`) с близкими датами, что предполагает проблемы с схемой.

#### 9. **Пароль студента может быть изменён учителем**
```csharp
// ? Возможно, учитель может менять пароли студентов
```

---

## ?? Рекомендации по улучшению

### ? Безопасность

1. **Добавить CORS политику**
   ```csharp
   builder.Services.AddCors(options =>
   {
       options.AddPolicy("AllowClient", policy =>
       {
           policy.WithOrigins("http://localhost:5173")  // или конкретный домен
                 .AllowAnyMethod()
                 .AllowAnyHeader();
       });
   });
   app.UseCors("AllowClient");
   ```

2. **Использовать Data Annotations для валидации**
   ```csharp
   public class CreateDictionaryRequest
   {
       [Required(ErrorMessage = "Имя словаря обязательно")]
       [StringLength(100, MinimumLength = 1)]
       public string Name { get; set; }
   }
   ```

3. **Реализовать глобальную обработку исключений**
   ```csharp
   app.UseExceptionHandler(exceptionHandlerApp =>
   {
       exceptionHandlerApp.Run(async context =>
       {
           var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
           // Log и return 500
       });
   });
   ```

4. **Использовать Role-Based Authorization**
   ```csharp
   [Authorize(Roles = "Teacher")]
   public async Task<IActionResult> CreateDictionary(...)
   ```

5. **Использовать HTTPS в production**
   ```csharp
   builder.Services.AddHsts(options =>
   {
       options.IncludeSubDomains = true;
       options.MaxAge = TimeSpan.FromDays(365);
   });
   ```

### ? Производительность

1. **Добавить пагинацию**
   ```csharp
   [HttpGet]
   public async Task<IActionResult> GetDictionaries(
       [FromQuery] int page = 1,
       [FromQuery] int pageSize = 10)
   {
       var query = _context.Dictionaries
           .Where(d => d.UserId == userId)
           .OrderByDescending(d => d.CreatedAt)
           .Skip((page - 1) * pageSize)
           .Take(pageSize);
   }
   ```

2. **Использовать Include для предотвращения N+1**
   ```csharp
   var dictionaries = await _context.Dictionaries
       .Include(d => d.Words)
       .Include(d => d.CreatedBy)
       .Where(d => d.UserId == userId)
       .ToListAsync();
   ```

3. **Кешировать часто запрашиваемые данные**
   ```csharp
   const string CACHE_KEY = "dictionaries_user_{userId}";
   if (!await _cache.GetStringAsync(CACHE_KEY, out var cached))
   {
       var data = await _context.Dictionaries.ToListAsync();
       await _cache.SetStringAsync(CACHE_KEY, JsonConvert.SerializeObject(data), 
           new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1) });
   }
   ```

4. **Использовать асинхронные операции повсеместно**
   ```csharp
   // ? Избегать
   var words = _context.Words.ToList();  // Синхронный
   
   // ? Правильно
   var words = await _context.Words.ToListAsync();
   ```

5. **Добавить индексы БД**
   ```csharp
   modelBuilder.Entity<LearningProgress>()
       .HasIndex(p => new { p.UserId, p.WordId })
       .IsUnique();
   
   modelBuilder.Entity<Dictionary>()
       .HasIndex(d => new { d.UserId, d.CreatedAt });
   ```

### ??? Архитектура и код

1. **Извлечь константы ролей в Enum**
   ```csharp
   public enum UserRole
   {
       Admin = 1,
       Teacher = 2,
       Student = 3
   }
   ```

2. **Создать BaseController с общей логикой**
   ```csharp
   public abstract class BaseApiController : ControllerBase
   {
       protected int GetUserId() 
       {
           var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
               ?? throw new UnauthorizedAccessException("User ID not found in claims");
           return int.Parse(userIdString);
       }
   }
   
   public class DictionaryController : BaseApiController { ... }
   ```

3. **Создать ApiResponse wrapper**
   ```csharp
   public class ApiResponse<T>
   {
       public bool Success { get; set; }
       public T Data { get; set; }
       public string Message { get; set; }
       public List<string> Errors { get; set; }
   }
   ```

4. **Слить TokenService в одно место (LearningTrainerShared)**
   ```
   LearningTrainerShared/
   ??? Services/
   ?   ??? TokenService.cs  // Одна реализация для обоих проектов
   ```

5. **Добавить Unit тесты**
   ```
   LearningTrainer.Tests/
   ??? Services/
       ??? ApiDataServiceTests.cs
       ??? SessionServiceTests.cs
   
   LearningAPI.Tests/
   ??? Controllers/
       ??? DictionaryControllerTests.cs
       ??? AuthControllerTests.cs
   ```

6. **Использовать FluentValidation для моделей**
   ```csharp
   public class CreateDictionaryRequestValidator : AbstractValidator<CreateDictionaryRequest>
   {
       public CreateDictionaryRequestValidator()
       {
           RuleFor(x => x.Name)
               .NotEmpty().WithMessage("Имя обязательно")
               .MaximumLength(100).WithMessage("Максимум 100 символов");
       }
   }
   ```

7. **Добавить Specification паттерн для сложных запросов**
   ```csharp
   public class UserWithDictionariesSpecification : Specification<User>
   {
       public UserWithDictionariesSpecification(int userId)
       {
           Query.Where(u => u.Id == userId)
               .Include(u => u.Dictionaries)
               .ThenInclude(d => d.Words);
       }
   }
   ```

### ?? Документация и тестирование

1. **Добавить XML комментарии в контроллеры**
   ```csharp
   /// <summary>
   /// Получает список словарей текущего пользователя
   /// </summary>
   /// <returns>Список словарей с их словами</returns>
   /// <response code="200">Успешно получены словари</response>
   /// <response code="401">Не авторизован</response>
   [HttpGet]
   public async Task<IActionResult> GetDictionaries()
   ```

2. **Добавить модульные тесты контроллеров**
   ```csharp
   [TestClass]
   public class DictionaryControllerTests
   {
       [TestMethod]
       public async Task GetDictionaries_WithValidUser_ReturnsOkResult()
       {
           // Arrange
           var mockContext = new Mock<ApiDbContext>();
           var controller = new DictionaryController(mockContext.Object, ...);
           
           // Act
           var result = await controller.GetDictionaries();
           
           // Assert
           Assert.IsInstanceOfType(result, typeof(OkObjectResult));
       }
   }
   ```

3. **Добавить интеграционные тесты**
   ```csharp
   [TestClass]
   public class AuthIntegrationTests
   {
       [TestInitialize]
       public void Setup()
       {
           _factory = new WebApplicationFactory<Program>();
           _client = _factory.CreateClient();
       }
       
       [TestMethod]
       public async Task Login_WithValidCredentials_ReturnsToken()
       {
           // ...
       }
   }
   ```

### ?? UI/UX улучшения (WPF)

1. **Добавить loading indicators**
2. **Улучшить error handling в UI**
3. **Добавить retry logic для сетевых ошибок**
4. **Реализовать offline mode с локальной синхронизацией**

### ?? DevOps

1. **Добавить Docker поддержку**
   ```dockerfile
   FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
   WORKDIR /src
   COPY . .
   RUN dotnet build
   RUN dotnet publish -o /app
   
   FROM mcr.microsoft.com/dotnet/aspnet:8.0
   WORKDIR /app
   COPY --from=build /app .
   ENTRYPOINT ["dotnet", "LearningAPI.dll"]
   ```

2. **Добавить GitHub Actions CI/CD**
3. **Добавить Healthcheck endpoint**
4. **Мониторинг логов и ошибок (Application Insights)**

### ?? Новые функции

1. **Сложность слов** — рейтинг сложности
2. **Категории словарей** — группировка по темам
3. **Произношение** — интеграция с Google/Azure Speech API
4. **Статистика прогресса** — графики улучшения
5. **Соревнования** — лидерборды между студентами
6. **Экспорт/импорт** — CSV, Excel, PDF
7. **API для мобильных приложений** — iOS/Android клиент
8. **WebSocket** — real-time уведомления о новых словарях

---

## ?? Заключение

Проект имеет **солидную архитектуру** с использованием современных паттернов (MVVM, CQRS, DI), но требует:

? **Обязательно:**
- Исправить SQL injection уязвимости
- Добавить валидацию входных данных
- Реализовать правильную обработку ошибок
- Настроить CORS и безопасность

?? **Желательно:**
- Добавить логирование и мониторинг
- Оптимизировать SQL запросы (Include, пагинация)
- Покрыть code тестами
- Слить дублирующийся код (TokenService)
- Добавить role-based authorization

? **Приятные добавления:**
- Docker поддержка
- Mobile API
- Расширенная статистика
- CI/CD pipeline

