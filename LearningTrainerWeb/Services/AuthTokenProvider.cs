using System.Net.Http.Headers;

namespace LearningTrainerWeb.Services;

/// <summary>
/// Централизованный провайдер токена авторизации (scoped — один на Blazor circuit).
/// Поддерживает автоматическое обновление токена при приближении к истечению.
/// </summary>
public class AuthTokenProvider
{
    private string? _token;
    private DateTime _expiresAtUtc;
    private bool _isRefreshing;

    /// <summary>
    /// Буфер до истечения токена — если до expiry осталось меньше, запускаем refresh.
    /// </summary>
    private static readonly TimeSpan RefreshBuffer = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Делегат для вызова refresh через AuthService (устанавливается AuthService при инициализации).
    /// Возвращает true если токен был обновлён успешно.
    /// </summary>
    public Func<Task<bool>>? RefreshCallback { get; set; }

    public string? Token
    {
        get => _token;
        set => _token = value;
    }

    public DateTime ExpiresAtUtc
    {
        get => _expiresAtUtc;
        set => _expiresAtUtc = value;
    }

    /// <summary>
    /// Проверяет, истёк ли (или скоро истечёт) текущий токен.
    /// </summary>
    public bool IsTokenExpiredOrExpiring =>
        string.IsNullOrEmpty(_token) || DateTime.UtcNow.Add(RefreshBuffer) >= _expiresAtUtc;

    /// <summary>
    /// Устанавливает Authorization header на HttpClient из текущего токена.
    /// Вызывается сервисами перед каждым HTTP-запросом.
    /// </summary>
    public void ApplyTo(HttpClient httpClient)
    {
        if (!string.IsNullOrEmpty(_token))
        {
            httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _token);
        }
        else
        {
            httpClient.DefaultRequestHeaders.Authorization = null;
        }
    }

    /// <summary>
    /// Проверяет срок действия токена и обновляет его при необходимости,
    /// затем устанавливает Authorization header.
    /// </summary>
    public async Task EnsureValidTokenAsync(HttpClient httpClient)
    {
        if (!string.IsNullOrEmpty(_token) && IsTokenExpiredOrExpiring && !_isRefreshing)
        {
            _isRefreshing = true;
            try
            {
                if (RefreshCallback != null)
                {
                    await RefreshCallback();
                }
            }
            finally
            {
                _isRefreshing = false;
            }
        }

        ApplyTo(httpClient);
    }
}
