using Polly;
using Polly.Extensions.Http;
using System.Net.Http;

namespace LearningTrainer.Services;

/// <summary>
/// Polly Resilience policies: retry + circuit breaker для HTTP-запросов WPF-клиента.
/// </summary>
public static class HttpPolicyFactory
{
    /// <summary>
    /// Retry с exponential backoff: 3 попытки (1s → 2s → 4s).
    /// Срабатывает на transient HTTP ошибки (5xx, 408, network errors).
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt - 1)),
                onRetry: (outcome, delay, retryCount, _) =>
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[Polly] Retry {retryCount} after {delay.TotalSeconds}s — " +
                        $"{outcome.Exception?.Message ?? outcome.Result?.StatusCode.ToString()}");
                });
    }

    /// <summary>
    /// Circuit breaker: после 5 подряд неудачных запросов — размыкается на 30 секунд.
    /// Все запросы в это время мгновенно получают BrokenCircuitException.
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromSeconds(30),
                onBreak: (outcome, breakDelay) =>
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[Polly] Circuit OPEN for {breakDelay.TotalSeconds}s — " +
                        $"{outcome.Exception?.Message ?? outcome.Result?.StatusCode.ToString()}");
                },
                onReset: () => System.Diagnostics.Debug.WriteLine("[Polly] Circuit CLOSED"),
                onHalfOpen: () => System.Diagnostics.Debug.WriteLine("[Polly] Circuit HALF-OPEN"));
    }

    /// <summary>
    /// Комбинированная policy: retry оборачивает circuit breaker.
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage> GetCombinedPolicy()
    {
        return Policy.WrapAsync(GetRetryPolicy(), GetCircuitBreakerPolicy());
    }
}
