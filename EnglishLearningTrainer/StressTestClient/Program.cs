using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

public class UserSessionDto
{
    public string AccessToken { get; set; }
}

public class Program
{
    private static readonly string ApiBaseUrl = "http://localhost:5076";
    private static readonly string Username = "1"; 
    private static readonly string Password = "1";

    private static readonly int CONCURRENT_USERS = 1000; 
    private static readonly int REQUESTS_PER_USER = 200; 

    private static readonly HttpClient _client = new HttpClient { BaseAddress = new Uri(ApiBaseUrl) };

    public static async Task Main(string[] args)
    {
        Console.WriteLine($"Запускаем стресс-тест на {ApiBaseUrl}...");
        Console.WriteLine($"Пользователей: {CONCURRENT_USERS}, Запросов на юзера: {REQUESTS_PER_USER}");
        Console.WriteLine("-------------------------------------------------");

        var stopwatch = Stopwatch.StartNew();

        var tasks = new ConcurrentBag<Task>();

        await Parallel.ForAsync(0, CONCURRENT_USERS, async (i, token) =>
        {
            try
            {
                await SimulateUserActivity(i);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Юзер {i}] КРИТИЧЕСКАЯ ОШИБКА: {ex.Message}");
            }
        });

        stopwatch.Stop();

        Console.WriteLine("-------------------------------------------------");
        Console.WriteLine("Стресс-тест ЗАВЕРШЕН.");
        Console.WriteLine($"Общее время: {stopwatch.Elapsed.TotalSeconds:F2} сек.");
        long totalRequests = CONCURRENT_USERS * REQUESTS_PER_USER;
        Console.WriteLine($"Всего запросов: {totalRequests}");
        Console.WriteLine($"Запросов в секунду (RPS): {totalRequests / stopwatch.Elapsed.TotalSeconds:F2}");
    }

    private static async Task SimulateUserActivity(int userId)
    {
        var loginRequest = new { Username = Username, Password = Password };
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"[Юзер {userId}] НЕ СМОГ ЗАЛОГИНИТЬСЯ!");
            return;
        }

        var session = await response.Content.ReadFromJsonAsync<UserSessionDto>();
        var token = session.AccessToken;

        using var userClient = new HttpClient { BaseAddress = new Uri(ApiBaseUrl) };
        userClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        for (int i = 0; i < REQUESTS_PER_USER; i++)
        {
            var dictResponse = await userClient.GetAsync("/api/dictionaries");
            if (!dictResponse.IsSuccessStatusCode)
                Console.WriteLine($"[Юзер {userId}] Ошибка GetDictionaries: {dictResponse.StatusCode}");


            await Task.Delay(50); 
        }
        Console.WriteLine($"[Юзер {userId}] Завершил сессию.");
    }
}

