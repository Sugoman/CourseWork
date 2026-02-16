using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace StressTestClient;

/// <summary>
/// Жёсткий стресс-тест LearningTrainer API v3.0
/// Фазы: разогрев → нарастающая нагрузка → пиковые всплески → устойчивая нагрузка.
/// Покрывает все эндпоинты API: auth, dictionaries, words, rules, marketplace,
/// training, progress, statistics, comments, downloads.
/// </summary>
public class Program
{
    // === КОНФИГУРАЦИЯ ===
    private static readonly string ApiBaseUrl = "http://localhost:5077";

    // Пользователи
    private static readonly int TOTAL_USERS = 1500;

    // Длительность фаз (секунды)
    private static readonly int WARMUP_SECONDS = 100;
    private static readonly int RAMP_SECONDS = 200;
    private static readonly int SPIKE_SECONDS = 150;
    private static readonly int SUSTAINED_SECONDS = 450;
    private static readonly int TOTAL_DURATION_SECONDS =
        WARMUP_SECONDS + RAMP_SECONDS + SPIKE_SECONDS + SUSTAINED_SECONDS;

    // Пользователей по фазам
    private static readonly int WARMUP_USERS = 200;
    private static readonly int RAMP_TARGET_USERS = 1000;
    private static readonly int SPIKE_USERS = 1500;

    // Параллельные действия в одном «тике» пользователя
    private static readonly int MAX_PARALLEL_ACTIONS = 4;

    // Вероятности действий (сумма = 100)
    private const int P_GET_DICTIONARIES = 12;
    private const int P_GET_DICTIONARY_DETAIL = 5;
    private const int P_GET_RULES = 10;
    private const int P_MARKETPLACE_DICTS = 10;
    private const int P_MARKETPLACE_RULES = 8;
    private const int P_MARKETPLACE_DETAIL = 5;
    private const int P_TRAINING_DAILY_PLAN = 8;
    private const int P_TRAINING_WORDS = 7;
    private const int P_PROGRESS_UPDATE = 6;
    private const int P_CREATE_WORD = 5;
    private const int P_CREATE_RULE = 3;
    private const int P_ADD_COMMENT = 4;
    private const int P_DOWNLOAD_CONTENT = 3;
    private const int P_VIEW_STATS = 5;
    private const int P_VIEW_STATS_DAILY = 3;
    private const int P_VIEW_STATS_DICTS = 3;
    private const int P_VIEW_ACHIEVEMENTS = 3;

    // === МЕТРИКИ ===
    private static readonly ConcurrentDictionary<string, long> _successCounts = new();
    private static readonly ConcurrentDictionary<string, long> _errorCounts = new();
    private static readonly ConcurrentDictionary<string, ConcurrentBag<double>> _responseTimesByAction = new();
    private static readonly ConcurrentBag<double> _allResponseTimes = new();
    private static long _totalRequests = 0;
    private static int _activeUsers = 0;
    private static long _totalBytesReceived = 0;

    // Общий список ID контента из маркетплейса для реалистичных запросов
    private static readonly ConcurrentBag<int> _knownMarketplaceDictIds = new();
    private static readonly ConcurrentBag<int> _knownMarketplaceRuleIds = new();

    [ThreadStatic]
    private static Random? t_random;
    private static Random Rng => t_random ??= new Random(Environment.CurrentManagedThreadId ^ Environment.TickCount);

    public static async Task Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        PrintHeader();

        if (!await CheckApiHealth())
        {
            Console.WriteLine("❌ API недоступен! Убедитесь что сервер запущен.");
            return;
        }
        Console.WriteLine("✅ API доступен. Начинаем тест...\n");

        // Регистрация пользователей (параллельно, пачками)
        var users = await CreateTestUsers();
        if (users.Count == 0)
        {
            Console.WriteLine("❌ Не удалось создать тестовых пользователей!");
            return;
        }
        Console.WriteLine($"\n👥 Создано {users.Count} тестовых пользователей\n");

        // Предварительный сбор ID контента из маркетплейса
        await SeedMarketplaceIds();

        var globalCts = new CancellationTokenSource(TimeSpan.FromSeconds(TOTAL_DURATION_SECONDS));
        var stopwatch = Stopwatch.StartNew();

        var progressTask = ShowProgressAsync(globalCts.Token, stopwatch);

        // === Фаза 1: Разогрев ===
        Console.WriteLine($"🔥 Фаза 1: Разогрев ({WARMUP_SECONDS}с, {WARMUP_USERS} пользователей)");
        var warmupTasks = users.Take(WARMUP_USERS)
            .Select(u => SimulateUserBehavior(u, globalCts.Token, minDelay: 200, maxDelay: 800))
            .ToList();

        await Task.Delay(TimeSpan.FromSeconds(WARMUP_SECONDS), globalCts.Token).ContinueWith(_ => { });

        // === Фаза 2: Нарастающая нагрузка ===
        Console.WriteLine($"\n📈 Фаза 2: Нарастание ({RAMP_SECONDS}с, до {RAMP_TARGET_USERS} пользователей)");
        var rampBatch = users.Skip(WARMUP_USERS).Take(RAMP_TARGET_USERS - WARMUP_USERS).ToList();
        var perUserDelay = RAMP_SECONDS * 1000 / Math.Max(rampBatch.Count, 1);
        var rampTasks = new List<Task>();
        foreach (var u in rampBatch)
        {
            if (globalCts.IsCancellationRequested) break;
            rampTasks.Add(SimulateUserBehavior(u, globalCts.Token, minDelay: 50, maxDelay: 500));
            await Task.Delay(perUserDelay, globalCts.Token).ContinueWith(_ => { });
        }

        // === Фаза 3: Пиковые всплески ===
        Console.WriteLine($"\n⚡ Фаза 3: Пиковые всплески ({SPIKE_SECONDS}с, {SPIKE_USERS} пользователей)");
        var spikeTasks = users.Skip(RAMP_TARGET_USERS).Take(SPIKE_USERS - RAMP_TARGET_USERS)
            .Select(u => SimulateUserBehavior(u, globalCts.Token, minDelay: 10, maxDelay: 150))
            .ToList();

        // Запускаем параллельные burst-волны
        for (int burst = 0; burst < 5 && !globalCts.IsCancellationRequested; burst++)
        {
            var burstTasks = users.Take(50)
                .Select(u => ExecuteBurst(u, globalCts.Token))
                .ToList();
            await Task.WhenAll(burstTasks).ContinueWith(_ => { });
            await Task.Delay(2000, globalCts.Token).ContinueWith(_ => { });
        }

        // === Фаза 4: Устойчивая нагрузка ===
        Console.WriteLine($"\n🏋️ Фаза 4: Устойчивая нагрузка ({SUSTAINED_SECONDS}с)");
        // Все пользователи уже работают, просто ждём
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(SUSTAINED_SECONDS), globalCts.Token);
        }
        catch (OperationCanceledException) { }

        stopwatch.Stop();
        globalCts.Cancel();

        await Task.Delay(1000);

        PrintResults(stopwatch.Elapsed);

        await CleanupTestData(users);
    }

    private static void PrintHeader()
    {
        Console.WriteLine(@"
╔══════════════════════════════════════════════════════════════╗
║          🚀 LearningTrainer Stress Test v3.0 HARD           ║
║     Фазовый тест: разогрев → нарастание → пики → нагрузка  ║
╚══════════════════════════════════════════════════════════════╝
");
        Console.WriteLine($"📊 Конфигурация:");
        Console.WriteLine($"   • API URL:            {ApiBaseUrl}");
        Console.WriteLine($"   • Макс. пользователей: {TOTAL_USERS}");
        Console.WriteLine($"   • Общая длительность: {TOTAL_DURATION_SECONDS} сек");
        Console.WriteLine($"   • Фазы: разогрев {WARMUP_SECONDS}с → нарастание {RAMP_SECONDS}с → пики {SPIKE_SECONDS}с → устойчивая {SUSTAINED_SECONDS}с");
        Console.WriteLine($"   • Макс. параллельных действий/пользователь: {MAX_PARALLEL_ACTIONS}");
        Console.WriteLine();
    }

    private static async Task<bool> CheckApiHealth()
    {
        try
        {
            using var client = CreateHttpClient();
            client.Timeout = TimeSpan.FromSeconds(5);
            var response = await client.GetAsync("/api/health");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private static HttpClient CreateHttpClient()
    {
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            MaxConnectionsPerServer = 200,
            EnableMultipleHttp2Connections = true
        };
        return new HttpClient(handler) { BaseAddress = new Uri(ApiBaseUrl) };
    }

    private static async Task SeedMarketplaceIds()
    {
        try
        {
            using var client = CreateHttpClient();
            var resp = await client.GetAsync("/api/marketplace/dictionaries?page=1&pageSize=50");
            if (resp.IsSuccessStatusCode)
            {
                var json = await resp.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("items", out var items))
                {
                    foreach (var item in items.EnumerateArray())
                    {
                        if (item.TryGetProperty("id", out var id))
                            _knownMarketplaceDictIds.Add(id.GetInt32());
                    }
                }
            }
            var resp2 = await client.GetAsync("/api/marketplace/rules?page=1&pageSize=50");
            if (resp2.IsSuccessStatusCode)
            {
                var json = await resp2.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("items", out var items))
                {
                    foreach (var item in items.EnumerateArray())
                    {
                        if (item.TryGetProperty("id", out var id))
                            _knownMarketplaceRuleIds.Add(id.GetInt32());
                    }
                }
            }
        }
        catch { }
    }

    private static async Task<List<TestUser>> CreateTestUsers()
    {
        var users = new ConcurrentBag<TestUser>();
        var failReasons = new ConcurrentDictionary<string, int>();

        Console.WriteLine("🔄 Регистрация тестовых пользователей...");

        // Один HttpClient на всю фазу регистрации — переиспользует TCP-соединения,
        // избегает TIME_WAIT при создании/закрытии сотен соединений
        using var sharedClient = CreateHttpClient();
        sharedClient.Timeout = TimeSpan.FromSeconds(30);

        var batches = Enumerable.Range(0, TOTAL_USERS)
            .Select(i => new TestUser
            {
                Id = i,
                Username = $"st_{Guid.NewGuid():N}"[..20],
                Email = $"st_{Guid.NewGuid():N}@test.local",
                Password = "StressTest123!"
            })
            .Chunk(50)
            .ToList();

        foreach (var batch in batches)
        {
            var tasks = batch.Select(async user =>
            {
                try
                {
                    var registerRequest = new
                    {
                        Username = user.Username,
                        Email = user.Email,
                        Password = user.Password
                    };
                    var regResponse = await sharedClient.PostAsJsonAsync("/api/auth/register", registerRequest);

                    if (!regResponse.IsSuccessStatusCode && (int)regResponse.StatusCode != 409)
                    {
                        failReasons.AddOrUpdate($"Register:{(int)regResponse.StatusCode}", 1, (_, c) => c + 1);
                        return;
                    }

                    // Retry логин до 3 раз
                    for (int attempt = 0; attempt < 3; attempt++)
                    {
                        try
                        {
                            var loginRequest = new { Username = user.Username, Password = user.Password };
                            var loginResponse = await sharedClient.PostAsJsonAsync("/api/auth/login", loginRequest);

                            if (loginResponse.IsSuccessStatusCode)
                            {
                                var session = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
                                user.Token = session?.AccessToken ?? "";
                                user.UserId = session?.UserId ?? 0;

                                if (!string.IsNullOrEmpty(user.Token))
                                {
                                    users.Add(user);
                                    return;
                                }
                                failReasons.AddOrUpdate("Login:EmptyToken", 1, (_, c) => c + 1);
                            }
                            else
                            {
                                failReasons.AddOrUpdate($"Login:{(int)loginResponse.StatusCode}", 1, (_, c) => c + 1);
                            }
                        }
                        catch (TaskCanceledException)
                        {
                            failReasons.AddOrUpdate("Login:Timeout", 1, (_, c) => c + 1);
                        }
                        catch (HttpRequestException ex)
                        {
                            failReasons.AddOrUpdate($"Login:Http:{ex.Message[..Math.Min(30, ex.Message.Length)]}", 1, (_, c) => c + 1);
                        }

                        await Task.Delay(300 * (attempt + 1));
                    }
                    failReasons.AddOrUpdate("Login:AllRetriesFailed", 1, (_, c) => c + 1);
                }
                catch (TaskCanceledException)
                {
                    failReasons.AddOrUpdate("Register:Timeout", 1, (_, c) => c + 1);
                }
                catch (HttpRequestException ex)
                {
                    failReasons.AddOrUpdate($"Register:Http:{ex.Message[..Math.Min(30, ex.Message.Length)]}", 1, (_, c) => c + 1);
                }
                catch (Exception ex)
                {
                    failReasons.AddOrUpdate($"Other:{ex.GetType().Name}", 1, (_, c) => c + 1);
                }
            });

            await Task.WhenAll(tasks);
            await Task.Delay(100);
            Console.Write(".");
        }

        Console.WriteLine();

        // Диагностика: показываем причины провалов
        if (failReasons.Count > 0)
        {
            Console.WriteLine("   ⚠️ Причины неудачных регистраций:");
            foreach (var (reason, count) in failReasons.OrderByDescending(kv => kv.Value))
            {
                Console.WriteLine($"      • {reason}: {count}");
            }
        }

        return users.ToList();
    }

    private static async Task SimulateUserBehavior(TestUser user, CancellationToken ct, int minDelay, int maxDelay)
    {
        Interlocked.Increment(ref _activeUsers);

        using var client = CreateHttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", user.Token);
        client.Timeout = TimeSpan.FromSeconds(30);

        await CreateInitialContent(user, client);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                // Запускаем несколько действий параллельно
                var parallelCount = Rng.Next(1, MAX_PARALLEL_ACTIONS + 1);
                var actionTasks = new List<Task>(parallelCount);

                for (int i = 0; i < parallelCount; i++)
                {
                    var action = ChooseRandomAction();
                    actionTasks.Add(ExecuteAndRecord(action, user, client));
                }

                await Task.WhenAll(actionTasks);

                var thinkTime = Rng.Next(minDelay, maxDelay);
                await Task.Delay(thinkTime, ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                RecordMetrics("UnhandledError", false, 0);
                if (Interlocked.Read(ref _totalRequests) < 100)
                    Console.WriteLine($"⚠️ [{user.Username}] {ex.Message}");
            }
        }

        Interlocked.Decrement(ref _activeUsers);
    }

    private static async Task ExecuteBurst(TestUser user, CancellationToken ct)
    {
        using var client = CreateHttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", user.Token);
        client.Timeout = TimeSpan.FromSeconds(15);

        // Burst: 10 быстрых запросов без пауз
        var tasks = Enumerable.Range(0, 10).Select(_ =>
        {
            var action = ChooseRandomAction();
            return ExecuteAndRecord(action, user, client);
        });
        await Task.WhenAll(tasks);
    }

    private static async Task ExecuteAndRecord(string action, TestUser user, HttpClient client)
    {
        var sw = Stopwatch.StartNew();
        bool success;
        try
        {
            success = await ExecuteAction(action, user, client);
        }
        catch
        {
            success = false;
        }
        sw.Stop();
        RecordMetrics(action, success, sw.Elapsed.TotalMilliseconds);
    }

    private static async Task CreateInitialContent(TestUser user, HttpClient client)
    {
        try
        {
            var dictRequest = new
            {
                Name = $"Dict_{user.Id}_{DateTime.UtcNow.Ticks}",
                Description = "Stress test dictionary",
                LanguageFrom = "English",
                LanguageTo = "Russian"
            };

            var response = await client.PostAsJsonAsync("/api/dictionaries", dictRequest);
            if (response.IsSuccessStatusCode)
            {
                var dict = await response.Content.ReadFromJsonAsync<DictionaryResponse>();
                user.DictionaryId = dict?.Id ?? 0;

                // Добавляем 10 слов для реалистичности
                var wordTasks = Enumerable.Range(0, 10).Select(async i =>
                {
                    var wordRequest = new
                    {
                        OriginalWord = $"w{i}_{Guid.NewGuid():N}"[..14],
                        Translation = $"перевод_{i}",
                        Example = $"Example sentence number {i}",
                        DictionaryId = user.DictionaryId
                    };
                    var wr = await client.PostAsJsonAsync("/api/words", wordRequest);
                    if (wr.IsSuccessStatusCode)
                    {
                        var word = await wr.Content.ReadFromJsonAsync<WordResponse>();
                        if (word?.Id > 0)
                            user.WordIds.Add(word.Id);
                    }
                });
                await Task.WhenAll(wordTasks);
            }

            var ruleRequest = new
            {
                Title = $"Rule_{user.Id}_{DateTime.UtcNow.Ticks}",
                Description = "Stress test rule with detailed content",
                MarkdownContent = "# Test Rule\n\n## Section 1\nThis is a comprehensive test rule.\n\n## Section 2\n- Point 1\n- Point 2\n- Point 3\n\n## Examples\n```\nExample code block\n```",
                Category = new[] { "Grammar", "Vocabulary", "Pronunciation", "Writing" }[Rng.Next(4)],
                DifficultyLevel = Rng.Next(1, 6)
            };
            var ruleResponse = await client.PostAsJsonAsync("/api/rules", ruleRequest);
            if (ruleResponse.IsSuccessStatusCode)
            {
                var rule = await ruleResponse.Content.ReadFromJsonAsync<RuleResponse>();
                user.RuleId = rule?.Id ?? 0;
            }
        }
        catch { }
    }

    private static string ChooseRandomAction()
    {
        var roll = Rng.Next(100);
        var cumulative = 0;

        cumulative += P_GET_DICTIONARIES;
        if (roll < cumulative) return "GetDictionaries";

        cumulative += P_GET_DICTIONARY_DETAIL;
        if (roll < cumulative) return "GetDictionaryDetail";

        cumulative += P_GET_RULES;
        if (roll < cumulative) return "GetRules";

        cumulative += P_MARKETPLACE_DICTS;
        if (roll < cumulative) return "MarketplaceDicts";

        cumulative += P_MARKETPLACE_RULES;
        if (roll < cumulative) return "MarketplaceRules";

        cumulative += P_MARKETPLACE_DETAIL;
        if (roll < cumulative) return "MarketplaceDetail";

        cumulative += P_TRAINING_DAILY_PLAN;
        if (roll < cumulative) return "TrainingDailyPlan";

        cumulative += P_TRAINING_WORDS;
        if (roll < cumulative) return "TrainingWords";

        cumulative += P_PROGRESS_UPDATE;
        if (roll < cumulative) return "ProgressUpdate";

        cumulative += P_CREATE_WORD;
        if (roll < cumulative) return "CreateWord";

        cumulative += P_CREATE_RULE;
        if (roll < cumulative) return "CreateRule";

        cumulative += P_ADD_COMMENT;
        if (roll < cumulative) return "AddComment";

        cumulative += P_DOWNLOAD_CONTENT;
        if (roll < cumulative) return "DownloadContent";

        cumulative += P_VIEW_STATS;
        if (roll < cumulative) return "ViewStats";

        cumulative += P_VIEW_STATS_DAILY;
        if (roll < cumulative) return "ViewStatsDaily";

        cumulative += P_VIEW_STATS_DICTS;
        if (roll < cumulative) return "ViewStatsDicts";

        return "ViewAchievements";
    }

    private static async Task<bool> ExecuteAction(string action, TestUser user, HttpClient client)
    {
        HttpResponseMessage response;

        switch (action)
        {
            case "GetDictionaries":
            {
                var page = Rng.Next(1, 5);
                response = await client.GetAsync($"/api/dictionaries?page={page}&pageSize=10");
                TrackResponseSize(response);
                return response.IsSuccessStatusCode;
            }

            case "GetDictionaryDetail":
            {
                if (user.DictionaryId > 0)
                {
                    response = await client.GetAsync($"/api/dictionaries/{user.DictionaryId}");
                    TrackResponseSize(response);
                    return response.IsSuccessStatusCode;
                }
                return false;
            }

            case "GetRules":
            {
                response = await client.GetAsync("/api/rules");
                TrackResponseSize(response);
                return response.IsSuccessStatusCode;
            }

            case "MarketplaceDicts":
            {
                var page = Rng.Next(1, 10);
                var searches = new[] { "", "english", "test", "grammar", "stress" };
                var search = searches[Rng.Next(searches.Length)];
                var url = string.IsNullOrEmpty(search)
                    ? $"/api/marketplace/dictionaries?page={page}&pageSize=10"
                    : $"/api/marketplace/dictionaries?page={page}&pageSize=10&search={search}";
                response = await client.GetAsync(url);
                TrackResponseSize(response);

                // Собираем ID для последующих запросов
                if (response.IsSuccessStatusCode)
                {
                    try
                    {
                        var json = await response.Content.ReadAsStringAsync();
                        using var doc = JsonDocument.Parse(json);
                        if (doc.RootElement.TryGetProperty("items", out var items))
                        {
                            foreach (var item in items.EnumerateArray())
                            {
                                if (item.TryGetProperty("id", out var id))
                                    _knownMarketplaceDictIds.Add(id.GetInt32());
                            }
                        }
                    }
                    catch { }
                }
                return response.IsSuccessStatusCode;
            }

            case "MarketplaceRules":
            {
                var page = Rng.Next(1, 10);
                var categories = new[] { "", "Grammar", "Vocabulary", "Pronunciation", "Writing" };
                var cat = categories[Rng.Next(categories.Length)];
                var url = string.IsNullOrEmpty(cat)
                    ? $"/api/marketplace/rules?page={page}&pageSize=8"
                    : $"/api/marketplace/rules?page={page}&pageSize=8&category={cat}";
                response = await client.GetAsync(url);
                TrackResponseSize(response);

                if (response.IsSuccessStatusCode)
                {
                    try
                    {
                        var json = await response.Content.ReadAsStringAsync();
                        using var doc = JsonDocument.Parse(json);
                        if (doc.RootElement.TryGetProperty("items", out var items))
                        {
                            foreach (var item in items.EnumerateArray())
                            {
                                if (item.TryGetProperty("id", out var id))
                                    _knownMarketplaceRuleIds.Add(id.GetInt32());
                            }
                        }
                    }
                    catch { }
                }
                return response.IsSuccessStatusCode;
            }

            case "MarketplaceDetail":
            {
                // Детали словаря или правила из маркетплейса
                var dictIds = _knownMarketplaceDictIds.ToArray();
                var ruleIds = _knownMarketplaceRuleIds.ToArray();

                if (Rng.Next(2) == 0 && dictIds.Length > 0)
                {
                    var id = dictIds[Rng.Next(dictIds.Length)];
                    response = await client.GetAsync($"/api/marketplace/dictionaries/{id}");

                    // Также загружаем комментарии (как делает реальный UI)
                    _ = client.GetAsync($"/api/marketplace/dictionaries/{id}/comments");
                }
                else if (ruleIds.Length > 0)
                {
                    var id = ruleIds[Rng.Next(ruleIds.Length)];
                    response = await client.GetAsync($"/api/marketplace/rules/{id}");
                    _ = client.GetAsync($"/api/marketplace/rules/{id}/comments");
                }
                else
                {
                    response = await client.GetAsync("/api/marketplace/dictionaries?page=1&pageSize=5");
                }
                TrackResponseSize(response);
                return response.IsSuccessStatusCode;
            }

            case "TrainingDailyPlan":
            {
                response = await client.GetAsync("/api/training/daily-plan");
                TrackResponseSize(response);
                return response.IsSuccessStatusCode;
            }

            case "TrainingWords":
            {
                var modes = new[] { "mixed", "review", "difficult", "new" };
                var mode = modes[Rng.Next(modes.Length)];
                var url = user.DictionaryId > 0
                    ? $"/api/training/words?mode={mode}&dictionaryId={user.DictionaryId}&limit=20"
                    : $"/api/training/words?mode={mode}&limit=20";
                response = await client.GetAsync(url);
                TrackResponseSize(response);
                return response.IsSuccessStatusCode;
            }

            case "ProgressUpdate":
            {
                var wordIds = user.WordIds.ToArray();
                if (wordIds.Length > 0)
                {
                    var wordId = wordIds[Rng.Next(wordIds.Length)];
                    var progressUpdate = new
                    {
                        WordId = wordId,
                        Quality = Rng.Next(0, 6)
                    };
                    response = await client.PostAsJsonAsync("/api/progress/update", progressUpdate);
                    return response.IsSuccessStatusCode;
                }
                return false;
            }

            case "CreateWord":
            {
                if (user.DictionaryId > 0)
                {
                    var wordRequest = new
                    {
                        OriginalWord = $"s{Rng.Next(10000)}_{Guid.NewGuid():N}"[..14],
                        Translation = $"тест_{Rng.Next(10000)}",
                        Example = "Auto-generated stress test word",
                        DictionaryId = user.DictionaryId
                    };
                    response = await client.PostAsJsonAsync("/api/words", wordRequest);
                    if (response.IsSuccessStatusCode)
                    {
                        var word = await response.Content.ReadFromJsonAsync<WordResponse>();
                        if (word?.Id > 0)
                            user.WordIds.Add(word.Id);
                    }
                    return response.IsSuccessStatusCode;
                }
                return false;
            }

            case "CreateRule":
            {
                var ruleRequest = new
                {
                    Title = $"StressRule_{Guid.NewGuid():N}"[..20],
                    Description = "Auto-created stress test rule",
                    MarkdownContent = $"# Stress Rule\n\nGenerated at {DateTime.UtcNow:O}\n\n## Content\nLorem ipsum dolor sit amet.",
                    Category = new[] { "Grammar", "Vocabulary", "Pronunciation" }[Rng.Next(3)],
                    DifficultyLevel = Rng.Next(1, 6)
                };
                response = await client.PostAsJsonAsync("/api/rules", ruleRequest);
                if (response.IsSuccessStatusCode)
                {
                    var rule = await response.Content.ReadFromJsonAsync<RuleResponse>();
                    if (rule?.Id > 0 && user.RuleId == 0)
                        user.RuleId = rule.Id;
                    user.CreatedRuleIds.Add(rule?.Id ?? 0);
                }
                return response.IsSuccessStatusCode;
            }

            case "AddComment":
            {
                var dictIds = _knownMarketplaceDictIds.ToArray();
                var ruleIds = _knownMarketplaceRuleIds.ToArray();

                var commentRequest = new
                {
                    Rating = Rng.Next(1, 6),
                    Text = $"Stress test review #{Rng.Next(100000)}: " +
                           new[] { "Great content!", "Very helpful", "Needs improvement", "Excellent material", "Average quality" }[Rng.Next(5)]
                };

                if (Rng.Next(2) == 0 && dictIds.Length > 0)
                {
                    var id = dictIds[Rng.Next(dictIds.Length)];
                    response = await client.PostAsJsonAsync($"/api/marketplace/dictionaries/{id}/comments", commentRequest);
                }
                else if (ruleIds.Length > 0)
                {
                    var id = ruleIds[Rng.Next(ruleIds.Length)];
                    response = await client.PostAsJsonAsync($"/api/marketplace/rules/{id}/comments", commentRequest);
                }
                else
                {
                    return false;
                }
                return response.IsSuccessStatusCode;
            }

            case "DownloadContent":
            {
                var dictIds = _knownMarketplaceDictIds.ToArray();
                var ruleIds = _knownMarketplaceRuleIds.ToArray();

                if (Rng.Next(2) == 0 && dictIds.Length > 0)
                {
                    var id = dictIds[Rng.Next(dictIds.Length)];
                    response = await client.PostAsync($"/api/marketplace/dictionaries/{id}/download", null);
                }
                else if (ruleIds.Length > 0)
                {
                    var id = ruleIds[Rng.Next(ruleIds.Length)];
                    response = await client.PostAsync($"/api/marketplace/rules/{id}/download", null);
                }
                else
                {
                    return false;
                }
                return response.IsSuccessStatusCode;
            }

            case "ViewStats":
            {
                var periods = new[] { "week", "month", "all" };
                var period = periods[Rng.Next(periods.Length)];
                response = await client.GetAsync($"/api/statistics?period={period}");
                TrackResponseSize(response);

                // Параллельно запрашиваем сводку (как делает UI)
                _ = client.GetAsync("/api/statistics/summary");
                return response.IsSuccessStatusCode;
            }

            case "ViewStatsDaily":
            {
                var days = new[] { 7, 14, 30, 90 }[Rng.Next(4)];
                response = await client.GetAsync($"/api/statistics/daily?days={days}");
                TrackResponseSize(response);
                return response.IsSuccessStatusCode;
            }

            case "ViewStatsDicts":
            {
                response = await client.GetAsync("/api/statistics/dictionaries");
                TrackResponseSize(response);
                return response.IsSuccessStatusCode;
            }

            case "ViewAchievements":
            {
                response = await client.GetAsync("/api/statistics/achievements");
                TrackResponseSize(response);

                // Также запрашиваем сложные слова
                _ = client.GetAsync("/api/statistics/difficult-words?limit=20");
                return response.IsSuccessStatusCode;
            }

            default:
                return false;
        }
    }

    private static void TrackResponseSize(HttpResponseMessage response)
    {
        if (response.Content.Headers.ContentLength.HasValue)
        {
            Interlocked.Add(ref _totalBytesReceived, response.Content.Headers.ContentLength.Value);
        }
    }

    private static void RecordMetrics(string action, bool success, double responseTimeMs)
    {
        Interlocked.Increment(ref _totalRequests);

        if (success)
        {
            _successCounts.AddOrUpdate(action, 1, (_, count) => count + 1);
        }
        else
        {
            _errorCounts.AddOrUpdate(action, 1, (_, count) => count + 1);
        }

        if (responseTimeMs > 0)
        {
            _allResponseTimes.Add(responseTimeMs);
            var bag = _responseTimesByAction.GetOrAdd(action, _ => new ConcurrentBag<double>());
            bag.Add(responseTimeMs);
        }
    }

    private static async Task ShowProgressAsync(CancellationToken ct, Stopwatch sw)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(2000, ct);

                var elapsed = sw.Elapsed.TotalSeconds;
                var total = Interlocked.Read(ref _totalRequests);
                var rps = total / Math.Max(elapsed, 1);
                var successTotal = _successCounts.Values.Sum();
                var successRate = total > 0 ? (double)successTotal / total * 100 : 0;
                var avgResponseTime = _allResponseTimes.Count > 0 ? _allResponseTimes.Average() : 0;
                var mbReceived = Interlocked.Read(ref _totalBytesReceived) / 1_048_576.0;

                // Определяем текущую фазу
                string phase;
                if (elapsed < WARMUP_SECONDS) phase = "🔥WARM";
                else if (elapsed < WARMUP_SECONDS + RAMP_SECONDS) phase = "📈RAMP";
                else if (elapsed < WARMUP_SECONDS + RAMP_SECONDS + SPIKE_SECONDS) phase = "⚡SPIKE";
                else phase = "🏋️SUST";

                Console.Write($"\r{phase} {elapsed:F0}s | 👥{_activeUsers} | " +
                              $"📊{total} req | ⚡{rps:F1} RPS | " +
                              $"✅{successRate:F1}% | ⏱️{avgResponseTime:F0}ms | " +
                              $"📦{mbReceived:F1}MB   ");
            }
            catch (OperationCanceledException) { break; }
        }
        Console.WriteLine();
    }

    private static void PrintResults(TimeSpan duration)
    {
        Console.WriteLine(@"
╔══════════════════════════════════════════════════════════════════════╗
║                   📊 РЕЗУЛЬТАТЫ СТРЕСС-ТЕСТА v3.0                    ║
╚══════════════════════════════════════════════════════════════════════╝
");

        var totalReqs = Interlocked.Read(ref _totalRequests);
        var totalSuccess = _successCounts.Values.Sum();
        var totalErrors = _errorCounts.Values.Sum();
        var successRate = totalReqs > 0 ? (double)totalSuccess / totalReqs * 100 : 0;
        var rps = totalReqs / duration.TotalSeconds;
        var mbReceived = Interlocked.Read(ref _totalBytesReceived) / 1_048_576.0;

        Console.WriteLine($"⏱️  Длительность:           {duration.TotalSeconds:F1} сек");
        Console.WriteLine($"📊 Всего запросов:         {totalReqs:N0}");
        Console.WriteLine($"✅ Успешных:               {totalSuccess:N0} ({successRate:F1}%)");
        Console.WriteLine($"❌ Ошибок:                 {totalErrors:N0}");
        Console.WriteLine($"⚡ Запросов в секунду:     {rps:F1} RPS");
        Console.WriteLine($"📦 Данных получено:        {mbReceived:F2} MB");
        Console.WriteLine($"🔀 Пропускная способность: {mbReceived / duration.TotalSeconds * 1024:F1} KB/s");

        if (_allResponseTimes.Count > 0)
        {
            var times = _allResponseTimes.OrderBy(t => t).ToList();
            var avg = times.Average();
            var min = times[0];
            var max = times[^1];
            var p50 = times[(int)(times.Count * 0.5)];
            var p90 = times[(int)(times.Count * 0.9)];
            var p95 = times[(int)(times.Count * 0.95)];
            var p99 = times[Math.Min((int)(times.Count * 0.99), times.Count - 1)];

            Console.WriteLine($"\n📈 Время ответа (мс):");
            Console.WriteLine($"   • Мин:        {min:F1}");
            Console.WriteLine($"   • Среднее:    {avg:F1}");
            Console.WriteLine($"   • Медиана:    {p50:F1}");
            Console.WriteLine($"   • 90-й %%:     {p90:F1}");
            Console.WriteLine($"   • 95-й %%:     {p95:F1}");
            Console.WriteLine($"   • 99-й %%:     {p99:F1}");
            Console.WriteLine($"   • Макс:       {max:F1}");
        }

        // Детальная статистика по каждому действию
        Console.WriteLine($"\n📋 Детальная статистика по действиям:");
        Console.WriteLine("   Действие                  Успешно    Ошибки    Средн.ms   P95 ms    Макс ms");
        Console.WriteLine("   ────────────────────────────────────────────────────────────────────────────");

        var allActions = _successCounts.Keys.Union(_errorCounts.Keys).Distinct();
        foreach (var act in allActions.OrderByDescending(a => _successCounts.GetValueOrDefault(a, 0)))
        {
            var s = _successCounts.GetValueOrDefault(act, 0);
            var e = _errorCounts.GetValueOrDefault(act, 0);

            var avgMs = 0.0;
            var p95Ms = 0.0;
            var maxMs = 0.0;
            if (_responseTimesByAction.TryGetValue(act, out var bag) && bag.Count > 0)
            {
                var sorted = bag.OrderBy(t => t).ToList();
                avgMs = sorted.Average();
                p95Ms = sorted[Math.Min((int)(sorted.Count * 0.95), sorted.Count - 1)];
                maxMs = sorted[^1];
            }

            Console.WriteLine($"   {act,-25} {s,8}   {e,8}   {avgMs,8:F1}   {p95Ms,8:F1}   {maxMs,8:F1}");
        }

        Console.WriteLine();
        PrintPerformanceGrade(rps, successRate, _allResponseTimes.Count > 0 ? _allResponseTimes.Average() : 0);
    }

    private static void PrintPerformanceGrade(double rps, double successRate, double avgResponseTime)
    {
        Console.WriteLine("🏆 ОЦЕНКА ПРОИЗВОДИТЕЛЬНОСТИ:");

        string grade;
        string emoji;

        if (successRate >= 99 && rps >= 200 && avgResponseTime < 50)
        {
            grade = "S (Выдающийся результат!)";
            emoji = "💎";
        }
        else if (successRate >= 98 && rps >= 150 && avgResponseTime < 100)
        {
            grade = "A+ (Превосходно!)";
            emoji = "🌟";
        }
        else if (successRate >= 95 && rps >= 100 && avgResponseTime < 200)
        {
            grade = "A (Отлично)";
            emoji = "✨";
        }
        else if (successRate >= 90 && rps >= 50 && avgResponseTime < 500)
        {
            grade = "B (Хорошо)";
            emoji = "👍";
        }
        else if (successRate >= 80 && rps >= 20)
        {
            grade = "C (Удовлетворительно)";
            emoji = "⚠️";
        }
        else if (successRate >= 60)
        {
            grade = "D (Требует оптимизации)";
            emoji = "🔧";
        }
        else
        {
            grade = "F (Критические проблемы)";
            emoji = "🔥";
        }

        Console.WriteLine($"   {emoji} {grade}");

        // Рекомендации
        Console.WriteLine("\n💡 Рекомендации:");
        if (avgResponseTime > 500)
            Console.WriteLine("   • ⚠️ Среднее время ответа >500мс — проверьте индексы БД и кэширование");
        if (successRate < 90)
            Console.WriteLine("   • ⚠️ Высокий процент ошибок — проверьте логи сервера");
        if (rps < 50)
            Console.WriteLine("   • ⚠️ Низкий RPS — рассмотрите горизонтальное масштабирование");

        // Топ медленных эндпоинтов
        var slowest = _responseTimesByAction
            .Where(kv => kv.Value.Count > 5)
            .Select(kv => (Action: kv.Key, Avg: kv.Value.Average()))
            .OrderByDescending(x => x.Avg)
            .Take(3)
            .ToList();

        if (slowest.Count > 0)
        {
            Console.WriteLine("\n🐌 Топ медленных эндпоинтов:");
            foreach (var (act, avg) in slowest)
                Console.WriteLine($"   • {act}: {avg:F1}мс среднее");
        }

        Console.WriteLine();
    }

    private static async Task CleanupTestData(List<TestUser> users)
    {
        Console.WriteLine("🧹 Очистка тестовых данных...");

        var deletedDicts = 0;
        var deletedRules = 0;

        // Параллельная очистка
        var tasks = users.Select(async user =>
        {
            try
            {
                using var client = CreateHttpClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", user.Token);

                if (user.DictionaryId > 0)
                {
                    var response = await client.DeleteAsync($"/api/dictionaries/{user.DictionaryId}");
                    if (response.IsSuccessStatusCode) Interlocked.Increment(ref deletedDicts);
                }

                if (user.RuleId > 0)
                {
                    var response = await client.DeleteAsync($"/api/rules/{user.RuleId}");
                    if (response.IsSuccessStatusCode) Interlocked.Increment(ref deletedRules);
                }

                foreach (var ruleId in user.CreatedRuleIds)
                {
                    if (ruleId > 0)
                    {
                        var response = await client.DeleteAsync($"/api/rules/{ruleId}");
                        if (response.IsSuccessStatusCode) Interlocked.Increment(ref deletedRules);
                    }
                }
            }
            catch { }
        });

        await Task.WhenAll(tasks);

        Console.WriteLine($"   Удалено словарей: {deletedDicts}");
        Console.WriteLine($"   Удалено правил: {deletedRules}");
        Console.WriteLine("✅ Очистка завершена\n");
    }
}

// === DTO классы ===

public class TestUser
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
    public string Token { get; set; } = "";
    public int UserId { get; set; }
    public int DictionaryId { get; set; }
    public int RuleId { get; set; }
    public ConcurrentBag<int> WordIds { get; set; } = new();
    public ConcurrentBag<int> CreatedRuleIds { get; set; } = new();
}

public class LoginResponse
{
    public int UserId { get; set; }
    public string? AccessToken { get; set; }
    public string? Username { get; set; }
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

public class WordResponse
{
    public int Id { get; set; }
    public string? OriginalWord { get; set; }
}


