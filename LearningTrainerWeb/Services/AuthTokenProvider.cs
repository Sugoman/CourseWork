using System.Net.Http.Headers;

namespace LearningTrainerWeb.Services;

/// <summary>
/// Централизованный провайдер токена авторизации (scoped — один на Blazor circuit).
/// Каждый API-сервис вызывает ApplyTo() перед запросом для установки заголовка Authorization.
/// </summary>
public class AuthTokenProvider
{
    private string? _token;

    public string? Token
    {
        get => _token;
        set => _token = value;
    }

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
}
