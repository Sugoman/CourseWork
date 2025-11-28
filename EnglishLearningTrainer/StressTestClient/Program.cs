using LearningTrainerShared.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
    private static readonly string ApiBaseUrl = "http://localhost:5077";
    private static readonly string Username = "123";
    private static readonly string Password = "1";

    private static readonly int CONCURRENT_USERS = 100;
    private static readonly int REQUESTS_PER_USER = 20;

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

    public static class DataGenerator
    {
        private static readonly Random Random = new Random();
        private static readonly string[] Words = { "apple", "banana", "cat", "dog", "house" };

        public static object CreateRandomWordRequest(int dictionaryId)
        {
            var originalWord = Words[Random.Next(Words.Length)] + Random.Next(1000); // Уникальное слово
            return new
            {
                OriginalWord = originalWord,
                Translation = "Случайный перевод",
                Example = "Случайный пример",
                DictionaryId = dictionaryId
            };
        }

        public static object CreateRandomDictionaryRequest()
        {
            return new
            {
                Name = $"ТестСловарь_{Guid.NewGuid().ToString().Substring(0, 8)}",
                Description = "Стресс-тест",
                LanguageFrom = "English",
                LanguageTo = "Russian"
            };
        }
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
        userClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // --- 1. СОЗДАЁМ СЛОВАРЬ (ОДИН РАЗ) ---
        var dictRequest = DataGenerator.CreateRandomDictionaryRequest();
        var dictResponse = await userClient.PostAsJsonAsync("/api/dictionaries", dictRequest);

        if (!dictResponse.IsSuccessStatusCode)
        {
            Console.WriteLine($"[Юзер {userId}] Ошибка создания словаря: {dictResponse.StatusCode}");
            return;
        }

        // Получаем ID созданного словаря для последующих запросов
        var createdDict = await dictResponse.Content.ReadFromJsonAsync<Dictionary>(); // Нужен класс Dictionary
        int testDictionaryId = createdDict.Id;

        // --- 2. ЦИКЛ ДОБАВЛЕНИЯ СЛОВ ---
        for (int i = 0; i < REQUESTS_PER_USER; i++)
        {
            var wordRequest = DataGenerator.CreateRandomWordRequest(testDictionaryId);
            await userClient.PostAsJsonAsync("/api/words", wordRequest); // Добавляем слово

            // Тут можно добавить GetAsync("/api/dictionaries/{testDictionaryId}") для чтения

            await Task.Delay(50); // Пауза, чтобы не убить базу
        }
        Console.WriteLine($"[Юзер {userId}] Завершил сессию. Добавлено {REQUESTS_PER_USER} слов.");
    }
}




