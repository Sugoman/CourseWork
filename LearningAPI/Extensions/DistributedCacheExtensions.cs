using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace LearningAPI.Extensions;

public static class DistributedCacheExtensions
{
    private static volatile bool _isAvailable = true;
    private static DateTime _retryAfter = DateTime.MinValue;
    private static readonly TimeSpan CircuitBreakDuration = TimeSpan.FromSeconds(30);

    // Simple console logging for debugging
    private static void LogDebug(string message)
    {
        Console.WriteLine($"[Cache] {DateTime.Now:HH:mm:ss} - {message}");
    }

    private static bool IsCircuitOpen()
    {
        if (_isAvailable)
            return false;

        if (DateTime.UtcNow >= _retryAfter)
        {
            _isAvailable = true;
            LogDebug("Circuit CLOSED - retrying Redis");
            return false;
        }

        return true;
    }

    private static void TripCircuit()
    {
        _isAvailable = false;
        _retryAfter = DateTime.UtcNow.Add(CircuitBreakDuration);
        LogDebug($"Circuit OPEN - Redis unavailable, retry after {_retryAfter:HH:mm:ss}");
    }

    public static async Task<string?> TryGetStringAsync(this IDistributedCache cache, string key)
    {
        if (IsCircuitOpen())
        {
            LogDebug($"GET {key} - skipped (circuit open)");
            return null;
        }

        try
        {
            var result = await cache.GetStringAsync(key);
            LogDebug($"GET {key} - {(result != null ? "HIT" : "MISS")}");
            return result;
        }
        catch (Exception ex)
        {
            LogDebug($"GET {key} - ERROR: {ex.Message}");
            TripCircuit();
            return null;
        }
    }

    public static async Task TrySetStringAsync(
        this IDistributedCache cache,
        string key,
        string value,
        DistributedCacheEntryOptions options)
    {
        if (IsCircuitOpen())
        {
            LogDebug($"SET {key} - skipped (circuit open)");
            return;
        }

        try
        {
            await cache.SetStringAsync(key, value, options);
            LogDebug($"SET {key} - OK ({value.Length} bytes)");
        }
        catch (Exception ex)
        {
            LogDebug($"SET {key} - ERROR: {ex.Message}");
            TripCircuit();
        }
    }

    public static async Task TryRemoveAsync(this IDistributedCache cache, string key)
    {
        if (IsCircuitOpen())
        {
            LogDebug($"DEL {key} - skipped (circuit open)");
            return;
        }

        try
        {
            await cache.RemoveAsync(key);
            LogDebug($"DEL {key} - OK");
        }
        catch (Exception ex)
        {
            LogDebug($"DEL {key} - ERROR: {ex.Message}");
            TripCircuit();
        }
    }
}
