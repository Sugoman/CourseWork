using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace LearningAPI.Extensions;

public static class DistributedCacheExtensions
{
    private static volatile bool _isAvailable = true;
    private static DateTime _retryAfter = DateTime.MinValue;
    private static readonly TimeSpan CircuitBreakDuration = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Префикс InstanceName, задаётся в AddStackExchangeRedisCache.
    /// Все ключи в Redis хранятся как "{InstanceName}{key}".
    /// </summary>
    private const string InstancePrefix = "LearningTrainerCache_";

    private static ILogger? _logger;

    /// <summary>
    /// Инициализирует логгер для кэш-расширений.
    /// Вызвать один раз при старте приложения.
    /// </summary>
    public static void InitializeLogger(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger("LearningAPI.Cache");
    }

    private static bool IsCircuitOpen()
    {
        if (_isAvailable)
            return false;

        if (DateTime.UtcNow >= _retryAfter)
        {
            _isAvailable = true;
            _logger?.LogInformation("Redis circuit breaker CLOSED — retrying");
            return false;
        }

        return true;
    }

    private static void TripCircuit()
    {
        _isAvailable = false;
        _retryAfter = DateTime.UtcNow.Add(CircuitBreakDuration);
        _logger?.LogWarning("Redis circuit breaker OPEN — retry after {RetryAfter:HH:mm:ss}", _retryAfter);
    }

    public static async Task<string?> TryGetStringAsync(this IDistributedCache cache, string key)
    {
        if (IsCircuitOpen())
        {
            _logger?.LogDebug("Cache GET {Key} — skipped (circuit open)", key);
            return null;
        }

        try
        {
            var result = await cache.GetStringAsync(key);
            _logger?.LogDebug("Cache GET {Key} — {Result}", key, result != null ? "HIT" : "MISS");
            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Cache GET {Key} — ERROR", key);
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
            _logger?.LogDebug("Cache SET {Key} — skipped (circuit open)", key);
            return;
        }

        try
        {
            await cache.SetStringAsync(key, value, options);
            _logger?.LogDebug("Cache SET {Key} — OK ({Bytes} bytes)", key, value.Length);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Cache SET {Key} — ERROR", key);
            TripCircuit();
        }
    }

    public static async Task TryRemoveAsync(this IDistributedCache cache, string key)
    {
        if (IsCircuitOpen())
        {
            _logger?.LogDebug("Cache DEL {Key} — skipped (circuit open)", key);
            return;
        }

        try
        {
            await cache.RemoveAsync(key);
            _logger?.LogDebug("Cache DEL {Key} — OK", key);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Cache DEL {Key} — ERROR", key);
            TripCircuit();
        }
    }

    /// <summary>
    /// Удаляет все ключи Redis, начинающиеся с указанного префикса.
    /// Использует SCAN (без блокировки сервера) + пакетный DELETE.
    /// Если IConnectionMultiplexer не передан (null) — fallback: ничего не делает.
    /// </summary>
    public static async Task TryRemoveByPrefixAsync(
        this IDistributedCache cache,
        string prefix,
        IConnectionMultiplexer? redis)
    {
        if (IsCircuitOpen() || redis is null)
            return;

        try
        {
            var fullPrefix = $"{InstancePrefix}{prefix}";
            var server = redis.GetServers().FirstOrDefault(s => s.IsConnected && !s.IsReplica);
            if (server is null)
                return;

            var keys = new List<RedisKey>();
            await foreach (var key in server.KeysAsync(pattern: $"{fullPrefix}*", pageSize: 100))
            {
                keys.Add(key);
            }

            if (keys.Count > 0)
            {
                var db = redis.GetDatabase();
                await db.KeyDeleteAsync(keys.ToArray());
                _logger?.LogInformation("Cache DEL prefix '{Prefix}*' — removed {Count} key(s)", prefix, keys.Count);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Cache DEL prefix '{Prefix}*' — ERROR", prefix);
            TripCircuit();
        }
    }
}
