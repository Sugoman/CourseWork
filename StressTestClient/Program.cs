using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace StressTestClient;

/// <summary>
/// Реалистичный стресс-тест LearningTrainer API
/// Имитирует поведение реальных пользователей: регистрация, создание контента,
/// обучение, работа с маркетплейсом, комментарии и т.д.
/// </summary>
public class Program
{
    // === КОНФИГУРАЦИЯ ===
    private static readonly string ApiBaseUrl = "http://localhost:5077";
    
    // Количество виртуальных пользователей
    private static readonly int TOTAL_USERS = 50;
    
    // Длительность теста в секундах
    private static readonly int TEST_DURATION_SECONDS = 60;
    
    // Вероятности действий (должны давать 100%)
    private static readonly int PROB_READ_DICTIONARIES = 25;      // Просмотр словарей
    private static readonly int PROB_READ_RULES = 15;             // Просмотр правил
    private static readonly int PROB_BROWSE_MARKETPLACE = 20;     // Маркетплейс
    private static readonly int PROB_LEARNING_SESSION = 15;       // Обучение
    private static readonly int PROB_CREATE_CONTENT = 10;         // Создание контента
    private static readonly int PROB_ADD_COMMENT = 5;             // Комментарии
    private static readonly int PROB_DOWNLOAD_CONTENT = 5;        // Скачивание
    private static readonly int PROB_VIEW_STATS = 5;              // Статистика

    // === МЕТРИКИ ===
    private static readonly ConcurrentDictionary<string, int> _successCounts = new();
    private static readonly ConcurrentDictionary<string, int> _errorCounts = new();
    private static readonly ConcurrentBag<double> _responseTimes = new();
    private static int _totalRequests = 0;
    private static int _activeUsers = 0;
    
    private static readonly Random _random = new();
    private static readonly object _lockObj = new();

    public static async Task Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        
        PrintHeader();
        
        // Проверка доступности API
        if (!await CheckApiHealth())
        {
            Console.WriteLine("❌ API недоступен! Убедитесь что сервер запущен.");
            return;
        }
        
        Console.WriteLine("✅ API доступен. Начинаем тест...\n");
        
        // Создаём тестовых пользователей
        var users = await CreateTestUsers();
        if (users.Count == 0)
        {
            Console.WriteLine("❌ Не удалось создать тестовых пользователей!");
            return;
        }
        
        Console.WriteLine($"👥 Создано {users.Count} тестовых пользователей\n");
        
        // Запускаем тест
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(TEST_DURATION_SECONDS));
        var stopwatch = Stopwatch.StartNew();
        
        var progressTask = ShowProgressAsync(cts.Token);
        
        var userTasks = users.Select(u => SimulateUserBehavior(u, cts.Token)).ToList();
        
        try
        {
            await Task.WhenAll(userTasks);
        }
        catch (OperationCanceledException)
        {
            // Нормальное завершение по таймеру
        }
        
        stopwatch.Stop();
        cts.Cancel();
        
        await Task.Delay(500); // Даём время на завершение
        
        PrintResults(stopwatch.Elapsed);
        
        // Очистка тестовых данных
        await CleanupTestData(users);
    }

    private static void PrintHeader()
    {
        Console.WriteLine(@"
╔══════════════════════════════════════════════════════════════╗
║           🚀 LearningTrainer Stress Test v2.0                ║
║              Реалистичная симуляция нагрузки                 ║
╚══════════════════════════════════════════════════════════════╝
");
        Console.WriteLine($"📊 Конфигурация:");
        Console.WriteLine($"   • API URL: {ApiBaseUrl}");
        Console.WriteLine($"   • Виртуальных пользователей: {TOTAL_USERS}");
        Console.WriteLine($"   • Длительность теста: {TEST_DURATION_SECONDS} сек");
        Console.WriteLine();
    }

    private static async Task<bool> CheckApiHealth()
    {
        try
        {
            using var client = new HttpClient { BaseAddress = new Uri(ApiBaseUrl) };
            client.Timeout = TimeSpan.FromSeconds(5);
            var response = await client.GetAsync("/api/health");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<List<TestUser>> CreateTestUsers()
    {
        var users = new List<TestUser>();
        
        Console.WriteLine("🔄 Регистрация тестовых пользователей...");
        
        for (int i = 0; i < TOTAL_USERS; i++)
        {
            var user = new TestUser
            {
                Id = i,
                Login = $"stress_user_{Guid.NewGuid():N}".Substring(0, 20),
                Password = "StressTest123!"
            };
            
            try
            {
                using var client = new HttpClient { BaseAddress = new Uri(ApiBaseUrl) };
                
                // Регистрация
                var registerRequest = new { Login = user.Login, Password = user.Password };
                var registerResponse = await client.PostAsJsonAsync("/api/auth/register", registerRequest);
                
                if (!registerResponse.IsSuccessStatusCode)
                {
                    // Может уже существует - пробуем войти
                }
                
                // Логин
                var loginRequest = new { Username = user.Login, Password = user.Password };
                var loginResponse = await client.PostAsJsonAsync("/api/auth/login", loginRequest);
                
                if (loginResponse.IsSuccessStatusCode)
                {
                    var session = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
                    user.Token = session?.AccessToken ?? "";
                    user.UserId = session?.UserId ?? 0;
                    
                    if (!string.IsNullOrEmpty(user.Token))
                    {
                        users.Add(user);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Ошибка создания пользователя {i}: {ex.Message}");
            }
            
            // Не перегружаем API регистрациями
            if (i % 10 == 0)
            {
                Console.Write(".");
            }
        }
        
        Console.WriteLine();
        return users;
    }

    private static async Task SimulateUserBehavior(TestUser user, CancellationToken ct)
    {
        Interlocked.Increment(ref _activeUsers);
        
        using var client = new HttpClient { BaseAddress = new Uri(ApiBaseUrl) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", user.Token);
        client.Timeout = TimeSpan.FromSeconds(30);
        
        // Создаём начальный контент для пользователя
        await CreateInitialContent(user, client);
        
        while (!ct.IsCancellationRequested)
        {
            try
            {
                // Выбираем случайное действие
                var action = ChooseRandomAction();
                
                var sw = Stopwatch.StartNew();
                var success = await ExecuteAction(action, user, client);
                sw.Stop();
                
                RecordMetrics(action, success, sw.Elapsed.TotalMilliseconds);
                
                // Имитация "человеческого" поведения - паузы между действиями
                var thinkTime = _random.Next(100, 2000);
                await Task.Delay(thinkTime, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                RecordMetrics("Error", false, 0);
                // Логируем только первые ошибки
                if (_errorCounts.Values.Sum() < 10)
                {
                    Console.WriteLine($"⚠️ [{user.Login}] {ex.Message}");
                }
            }
        }
        
        Interlocked.Decrement(ref _activeUsers);
    }

    private static async Task CreateInitialContent(TestUser user, HttpClient client)
    {
        try
        {
            // Создаём словарь
            var dictRequest = new
            {
                Name = $"Dict_{user.Id}_{DateTime.Now.Ticks}",
                Description = "Stress test dictionary",
                LanguageFrom = "English",
                LanguageTo = "Russian"
            };
            
            var response = await client.PostAsJsonAsync("/api/dictionaries", dictRequest);
            if (response.IsSuccessStatusCode)
            {
                var dict = await response.Content.ReadFromJsonAsync<DictionaryResponse>();
                user.DictionaryId = dict?.Id ?? 0;
                
                // Добавляем несколько слов
                for (int i = 0; i < 5; i++)
                {
                    var wordRequest = new
                    {
                        OriginalWord = $"word_{i}_{Guid.NewGuid():N}".Substring(0, 15),
                        Translation = $"перевод_{i}",
                        Example = "Example sentence",
                        DictionaryId = user.DictionaryId
                    };
                    await client.PostAsJsonAsync("/api/words", wordRequest);
                }
            }
            
            // Создаём правило
            var ruleRequest = new
            {
                Title = $"Rule_{user.Id}_{DateTime.Now.Ticks}",
                Description = "Stress test rule",
                MarkdownContent = "# Test Rule\n\nThis is a test rule for stress testing.",
                Category = "Grammar",
                DifficultyLevel = 2
            };
            
            var ruleResponse = await client.PostAsJsonAsync("/api/rules", ruleRequest);
            if (ruleResponse.IsSuccessStatusCode)
            {
                var rule = await ruleResponse.Content.ReadFromJsonAsync<RuleResponse>();
                user.RuleId = rule?.Id ?? 0;
            }
        }
        catch
        {
            // Игнорируем ошибки инициализации
        }
    }

    private static string ChooseRandomAction()
    {
        var roll = _random.Next(100);
        var cumulative = 0;
        
        cumulative += PROB_READ_DICTIONARIES;
        if (roll < cumulative) return "GetDictionaries";
        
        cumulative += PROB_READ_RULES;
        if (roll < cumulative) return "GetRules";
        
        cumulative += PROB_BROWSE_MARKETPLACE;
        if (roll < cumulative) return "BrowseMarketplace";
        
        cumulative += PROB_LEARNING_SESSION;
        if (roll < cumulative) return "LearningSession";
        
        cumulative += PROB_CREATE_CONTENT;
        if (roll < cumulative) return "CreateContent";
        
        cumulative += PROB_ADD_COMMENT;
        if (roll < cumulative) return "AddComment";
        
        cumulative += PROB_DOWNLOAD_CONTENT;
        if (roll < cumulative) return "DownloadContent";
        
        return "ViewStats";
    }

    private static async Task<bool> ExecuteAction(string action, TestUser user, HttpClient client)
    {
        HttpResponseMessage response;
        
        switch (action)
        {
            case "GetDictionaries":
                response = await client.GetAsync("/api/dictionaries");
                return response.IsSuccessStatusCode;
                
            case "GetRules":
                response = await client.GetAsync("/api/rules");
                return response.IsSuccessStatusCode;
                
            case "BrowseMarketplace":
                // Случайный просмотр маркетплейса
                var page = _random.Next(1, 5);
                var endpoints = new[]
                {
                    $"/api/marketplace/dictionaries?page={page}&pageSize=10",
                    $"/api/marketplace/rules?page={page}&pageSize=10",
                };
                response = await client.GetAsync(endpoints[_random.Next(endpoints.Length)]);
                return response.IsSuccessStatusCode;
                
            case "LearningSession":
                if (user.DictionaryId > 0)
                {
                    response = await client.GetAsync($"/api/progress/session/{user.DictionaryId}");
                    if (response.IsSuccessStatusCode)
                    {
                        // Имитируем ответ на слово
                        var progressUpdate = new
                        {
                            WordId = 1, // В реальности нужно ID из сессии
                            Quality = _random.Next(0, 4)
                        };
                        await client.PostAsJsonAsync("/api/progress/update", progressUpdate);
                    }
                    return response.IsSuccessStatusCode;
                }
                return false;
                
            case "CreateContent":
                // Создаём новое слово
                if (user.DictionaryId > 0)
                {
                    var wordRequest = new
                    {
                        OriginalWord = $"stress_{Guid.NewGuid():N}".Substring(0, 12),
                        Translation = "стресс-тест",
                        Example = "Stress test example",
                        DictionaryId = user.DictionaryId
                    };
                    response = await client.PostAsJsonAsync("/api/words", wordRequest);
                    return response.IsSuccessStatusCode;
                }
                return false;
                
            case "AddComment":
                // Добавляем комментарий к случайному контенту в маркетплейсе
                var commentRequest = new
                {
                    Rating = _random.Next(1, 6),
                    Text = $"Stress test comment {DateTime.Now.Ticks}"
                };
                // Пробуем добавить к первому словарю в маркетплейсе
                response = await client.PostAsJsonAsync("/api/marketplace/dictionaries/1/comments", commentRequest);
                return response.IsSuccessStatusCode;
                
            case "DownloadContent":
                // Скачиваем случайный контент
                response = await client.PostAsync($"/api/marketplace/dictionaries/1/download", null);
                return response.IsSuccessStatusCode;
                
            case "ViewStats":
                response = await client.GetAsync("/api/progress/stats");
                return response.IsSuccessStatusCode;
                
            default:
                return false;
        }
    }

    private static void RecordMetrics(string action, bool success, double responseTimeMs)
    {
        Interlocked.Increment(ref _totalRequests);
        
        if (success)
        {
            _successCounts.AddOrUpdate(action, 1, (_, count) => count + 1);
            _responseTimes.Add(responseTimeMs);
        }
        else
        {
            _errorCounts.AddOrUpdate(action, 1, (_, count) => count + 1);
        }
    }

    private static async Task ShowProgressAsync(CancellationToken ct)
    {
        var startTime = DateTime.Now;
        
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(2000, ct);
                
                var elapsed = (DateTime.Now - startTime).TotalSeconds;
                var rps = _totalRequests / Math.Max(elapsed, 1);
                var successRate = _totalRequests > 0 
                    ? (double)_successCounts.Values.Sum() / _totalRequests * 100 
                    : 0;
                var avgResponseTime = _responseTimes.Count > 0 
                    ? _responseTimes.Average() 
                    : 0;
                
                Console.Write($"\r⏱️ {elapsed:F0}s | 👥 {_activeUsers} active | " +
                              $"📊 {_totalRequests} req | ⚡ {rps:F1} RPS | " +
                              $"✅ {successRate:F1}% | ⏱️ {avgResponseTime:F0}ms avg   ");
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
        
        Console.WriteLine();
    }

    private static void PrintResults(TimeSpan duration)
    {
        Console.WriteLine(@"
╔══════════════════════════════════════════════════════════════╗
║                    📊 РЕЗУЛЬТАТЫ ТЕСТА                       ║
╚══════════════════════════════════════════════════════════════╝
");
        
        var totalSuccess = _successCounts.Values.Sum();
        var totalErrors = _errorCounts.Values.Sum();
        var successRate = _totalRequests > 0 ? (double)totalSuccess / _totalRequests * 100 : 0;
        var rps = _totalRequests / duration.TotalSeconds;
        
        Console.WriteLine($"⏱️  Длительность:           {duration.TotalSeconds:F1} сек");
        Console.WriteLine($"📊 Всего запросов:         {_totalRequests:N0}");
        Console.WriteLine($"✅ Успешных:               {totalSuccess:N0} ({successRate:F1}%)");
        Console.WriteLine($"❌ Ошибок:                 {totalErrors:N0}");
        Console.WriteLine($"⚡ Запросов в секунду:     {rps:F1} RPS");
        
        if (_responseTimes.Count > 0)
        {
            var times = _responseTimes.OrderBy(t => t).ToList();
            var avg = times.Average();
            var p50 = times[(int)(times.Count * 0.5)];
            var p95 = times[(int)(times.Count * 0.95)];
            var p99 = times[(int)(times.Count * 0.99)];
            
            Console.WriteLine($"\n📈 Время ответа (мс):");
            Console.WriteLine($"   • Среднее:    {avg:F1}");
            Console.WriteLine($"   • Медиана:    {p50:F1}");
            Console.WriteLine($"   • 95-й %%:     {p95:F1}");
            Console.WriteLine($"   • 99-й %%:     {p99:F1}");
        }
        
        Console.WriteLine($"\n📋 Статистика по действиям:");
        Console.WriteLine("   Действие                 Успешно    Ошибки");
        Console.WriteLine("   ─────────────────────────────────────────");
        
        var allActions = _successCounts.Keys.Union(_errorCounts.Keys).Distinct();
        foreach (var action in allActions.OrderByDescending(a => _successCounts.GetValueOrDefault(a, 0)))
        {
            var success = _successCounts.GetValueOrDefault(action, 0);
            var errors = _errorCounts.GetValueOrDefault(action, 0);
            Console.WriteLine($"   {action,-23} {success,8}   {errors,8}");
        }
        
        Console.WriteLine();
        
        // Оценка производительности
        PrintPerformanceGrade(rps, successRate, _responseTimes.Count > 0 ? _responseTimes.Average() : 0);
    }

    private static void PrintPerformanceGrade(double rps, double successRate, double avgResponseTime)
    {
        Console.WriteLine("🏆 ОЦЕНКА ПРОИЗВОДИТЕЛЬНОСТИ:");
        
        string grade;
        string emoji;
        
        if (successRate >= 99 && rps >= 100 && avgResponseTime < 100)
        {
            grade = "A+ (Отлично!)";
            emoji = "🌟";
        }
        else if (successRate >= 95 && rps >= 50 && avgResponseTime < 200)
        {
            grade = "A (Хорошо)";
            emoji = "✨";
        }
        else if (successRate >= 90 && rps >= 20 && avgResponseTime < 500)
        {
            grade = "B (Нормально)";
            emoji = "👍";
        }
        else if (successRate >= 80)
        {
            grade = "C (Удовлетворительно)";
            emoji = "⚠️";
        }
        else
        {
            grade = "D (Требует оптимизации)";
            emoji = "🔧";
        }
        
        Console.WriteLine($"   {emoji} {grade}");
        Console.WriteLine();
    }

    private static async Task CleanupTestData(List<TestUser> users)
    {
        Console.WriteLine("🧹 Очистка тестовых данных...");
        
        var deletedDicts = 0;
        var deletedRules = 0;
        
        foreach (var user in users)
        {
            try
            {
                using var client = new HttpClient { BaseAddress = new Uri(ApiBaseUrl) };
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", user.Token);
                
                if (user.DictionaryId > 0)
                {
                    var response = await client.DeleteAsync($"/api/dictionaries/{user.DictionaryId}");
                    if (response.IsSuccessStatusCode) deletedDicts++;
                }
                
                if (user.RuleId > 0)
                {
                    var response = await client.DeleteAsync($"/api/rules/{user.RuleId}");
                    if (response.IsSuccessStatusCode) deletedRules++;
                }
            }
            catch
            {
                // Игнорируем ошибки очистки
            }
        }
        
        Console.WriteLine($"   Удалено словарей: {deletedDicts}");
        Console.WriteLine($"   Удалено правил: {deletedRules}");
        Console.WriteLine("✅ Очистка завершена\n");
    }
}

// === DTO классы ===

public class TestUser
{
    public int Id { get; set; }
    public string Login { get; set; } = "";
    public string Password { get; set; } = "";
    public string Token { get; set; } = "";
    public int UserId { get; set; }
    public int DictionaryId { get; set; }
    public int RuleId { get; set; }
}

public class LoginResponse
{
    public int UserId { get; set; }
    public string? AccessToken { get; set; }
    public string? UserLogin { get; set; }
    public string? UserRole { get; set; }
}

public class DictionaryResponse
{
    public int Id { get; set; }
    public string? Name { get; set; }
}

public class RuleResponse
{
    public int Id { get; set; }
    public string? Title { get; set; }
}




