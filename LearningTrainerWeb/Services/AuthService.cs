using Microsoft.JSInterop;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace LearningTrainerWeb.Services;

/// <summary>
/// Сервис аутентификации пользователей
/// </summary>
public interface IAuthService
{
    Task<bool> LoginAsync(string username, string password, bool rememberMe);
    Task<bool> RegisterAsync(string username, string email, string password, string termsVersion);
    Task LogoutAsync();
    Task<UserSession?> GetCurrentSessionAsync();
    Task InitializeAsync();
    string? GetToken();
    /// <summary>
    /// Refreshes the access token using the stored refresh token.
    /// Returns true if the token was refreshed successfully.
    /// </summary>
    Task<bool> RefreshAccessTokenAsync();
    /// <summary>
    /// Updates the current session's access token and role without requiring re-login.
    /// </summary>
    Task UpdateSessionAsync(string newAccessToken, string newRole);

    /// <summary>
    /// Changes the user's password via the API.
    /// </summary>
    Task<(bool Success, string Message)> ChangePasswordAsync(string oldPassword, string newPassword);
}

public class AuthService : IAuthService
{
    private readonly HttpClient _httpClient;
    private readonly IJSRuntime _js;
    private readonly AuthTokenProvider _tokenProvider;
    private readonly ILogger<AuthService> _logger;
    private UserSession? _currentSession;
    private bool _isInitialized;
    /// <summary>
    /// Plain localStorage key for persistent "Remember me" sessions.
    /// NOT encrypted — survives container rebuilds without DP key dependency.
    /// </summary>
    private const string PersistentKey = "ingat_auth";

    public AuthService(
        HttpClient httpClient,
        IJSRuntime js,
        AuthTokenProvider tokenProvider,
        ILogger<AuthService> logger)
    {
        _httpClient = httpClient;
        _js = js;
        _tokenProvider = tokenProvider;
        _logger = logger;
    }

    public string? GetToken() => _currentSession?.Token;

    public async Task InitializeAsync()
    {
        if (_isInitialized) return;

        try
        {
            // Read from plain localStorage (no DP encryption — survives container rebuilds)
            var json = await _js.InvokeAsync<string?>("authStorageGet", PersistentKey);
            if (!string.IsNullOrEmpty(json))
            {
                var session = JsonSerializer.Deserialize<UserSession>(json);
                if (session != null && !string.IsNullOrEmpty(session.Token))
                {
                    _currentSession = session;
                    SetAuthHeaders(_currentSession.Token, _currentSession.ExpiresAtUtc);
                    _logger.LogDebug("Restored session for user {UserId} from localStorage", session.UserId);
                }
            }

            // If token is restored but expired — try refresh
            if (_currentSession != null && _currentSession.ExpiresAtUtc <= DateTime.UtcNow)
            {
                _logger.LogInformation("Restored token is expired, attempting refresh");
                var refreshed = await RefreshAccessTokenAsync();
                if (!refreshed)
                {
                    _logger.LogWarning("Token refresh failed — clearing session");
                    _currentSession = null;
                    _tokenProvider.Token = null;
                    await RemovePersistentSession();
                }
            }

            _isInitialized = true;
        }
        catch (InvalidOperationException)
        {
            // Expected during Blazor prerendering — JS interop not available.
            // Do NOT set _isInitialized = true so InitializeAsync retries on interactive render.
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error restoring auth session during initialization");
            await RemovePersistentSession();
            _isInitialized = true;
        }
    }

    #region Plain localStorage helpers

    private async Task SavePersistentSession()
    {
        try
        {
            var json = JsonSerializer.Serialize(_currentSession);
            await _js.InvokeVoidAsync("authStorageSet", PersistentKey, json);
        }
        catch { /* JS interop not available during prerendering */ }
    }

    private async Task RemovePersistentSession()
    {
        try
        {
            await _js.InvokeVoidAsync("authStorageRemove", PersistentKey);
        }
        catch { /* best effort */ }
    }

    private async Task<bool> HasPersistentSession()
    {
        try
        {
            var json = await _js.InvokeAsync<string?>("authStorageGet", PersistentKey);
            return !string.IsNullOrEmpty(json);
        }
        catch { return false; }
    }

    #endregion

    private void SetAuthHeaders(string? token, DateTime expiresAtUtc = default)
    {
        _tokenProvider.Token = token;
        _tokenProvider.ExpiresAtUtc = expiresAtUtc;
        _tokenProvider.RefreshCallback = RefreshAccessTokenAsync;
    }

    public async Task<bool> LoginAsync(string username, string password, bool rememberMe)
    {
        var request = new { Username = username, Password = password };
        var response = await _httpClient.PostAsJsonAsync("api/auth/login", request);

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
            if (result != null)
            {
                _currentSession = new UserSession
                    {
                        UserId = result.UserId,
                        UserName = result.Username ?? username,
                        Email = result.Email ?? "",
                        Token = result.AccessToken,
                        RefreshToken = result.RefreshToken,
                        Role = result.UserRole ?? "",
                        RememberMe = rememberMe,
                        ExpiresAtUtc = DateTime.UtcNow.AddSeconds(result.ExpiresIn > 0 ? result.ExpiresIn : 7200)
                    };

                SetAuthHeaders(result.AccessToken, _currentSession.ExpiresAtUtc);

                // Always save to plain localStorage (session will be cleared on logout)
                await SavePersistentSession();

                return true;
            }
        }
        return false;
    }

    public async Task<bool> RegisterAsync(string username, string email, string password, string termsVersion)
    {
        var request = new 
        { 
            Username = username,
            Email = email,
            Password = password,
            AcceptedTermsVersion = termsVersion
        };
        var response = await _httpClient.PostAsJsonAsync("api/auth/register", request);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> RefreshAccessTokenAsync()
    {
        if (_currentSession == null || string.IsNullOrEmpty(_currentSession.RefreshToken))
        {
            _logger.LogDebug("Cannot refresh token: no current session or refresh token");
            return false;
        }

        try
        {
            var request = new { RefreshToken = _currentSession.RefreshToken };
            var response = await _httpClient.PostAsJsonAsync("api/token/refresh", request);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Token refresh failed with status {StatusCode}", response.StatusCode);

                // If refresh token is invalid/expired — force logout
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    _logger.LogInformation("Refresh token expired or revoked — logging out");
                    await LogoutAsync();
                }
                return false;
            }

            var result = await response.Content.ReadFromJsonAsync<RefreshTokenResponse>();
            if (result == null || string.IsNullOrEmpty(result.AccessToken))
            {
                _logger.LogWarning("Token refresh returned empty response");
                return false;
            }

            // Update session with new tokens
            _currentSession.Token = result.AccessToken;
            _currentSession.RefreshToken = result.RefreshToken;
            _currentSession.ExpiresAtUtc = DateTime.UtcNow.AddSeconds(result.ExpiresIn > 0 ? result.ExpiresIn : 7200);

            SetAuthHeaders(_currentSession.Token, _currentSession.ExpiresAtUtc);

            // Persist updated session
            await SavePersistentSession();

            _logger.LogDebug("Access token refreshed successfully for user {UserId}", _currentSession.UserId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing access token");
            return false;
        }
    }

    public async Task LogoutAsync()
    {
        _currentSession = null;
        SetAuthHeaders(null);
        await RemovePersistentSession();
    }

    public async Task UpdateSessionAsync(string newAccessToken, string newRole)
    {
        if (_currentSession == null) return;

        _currentSession.Token = newAccessToken;
        _currentSession.Role = newRole;
        _currentSession.ExpiresAtUtc = DateTime.UtcNow.AddHours(2);

        SetAuthHeaders(_currentSession.Token, _currentSession.ExpiresAtUtc);
        await SavePersistentSession();
    }

    public async Task<UserSession?> GetCurrentSessionAsync()
    {
        if (!_isInitialized)
        {
            await InitializeAsync();
        }
        return _currentSession;
    }

    public async Task<(bool Success, string Message)> ChangePasswordAsync(string oldPassword, string newPassword)
    {
        try
        {
            await _tokenProvider.EnsureValidTokenAsync(_httpClient);
            var request = new { OldPassword = oldPassword, NewPassword = newPassword };
            var response = await _httpClient.PostAsJsonAsync("api/auth/change-password", request);

            if (response.IsSuccessStatusCode)
            {
                return (true, "Пароль успешно обновлён");
            }

            var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
            return (false, error?.Message ?? "Ошибка при смене пароля");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing password");
            return (false, "Ошибка соединения с сервером");
        }
    }

    private class ErrorResponse { public string? Message { get; set; } }

    private class LoginResponse
    {
        public int UserId { get; set; }
        public string? Username { get; set; }
        public string? Email { get; set; }
        public string AccessToken { get; set; } = "";
        public string RefreshToken { get; set; } = "";
        public int ExpiresIn { get; set; }
        public string? UserRole { get; set; }
    }

    private class RefreshTokenResponse
    {
        public string AccessToken { get; set; } = "";
        public string RefreshToken { get; set; } = "";
        public int ExpiresIn { get; set; }
        public string TokenType { get; set; } = "";
    }
}

public class UserSession
{
    public int UserId { get; set; }
    public string UserName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Token { get; set; } = "";
    public string RefreshToken { get; set; } = "";
    public string Role { get; set; } = "";
    public bool RememberMe { get; set; }
    /// <summary>
    /// UTC time when the access token expires.
    /// </summary>
    public DateTime ExpiresAtUtc { get; set; }
}
