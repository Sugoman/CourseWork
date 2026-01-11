using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using System.Net.Http.Json;

namespace LearningTrainerWeb.Services;

/// <summary>
/// Сервис аутентификации пользователей
/// </summary>
public interface IAuthService
{
    Task<bool> LoginAsync(string username, string password, bool rememberMe);
    Task<bool> RegisterAsync(string username, string email, string password, string? inviteCode);
    Task LogoutAsync();
    Task<UserSession?> GetCurrentSessionAsync();
    Task InitializeAsync();
    string? GetToken();
}

public class AuthService : IAuthService
{
    private readonly HttpClient _httpClient;
    private readonly ProtectedSessionStorage _sessionStorage;
    private readonly IContentApiService _contentApiService;
    private UserSession? _currentSession;
    private bool _isInitialized;
    private const string SessionKey = "auth_session";

    public AuthService(HttpClient httpClient, ProtectedSessionStorage sessionStorage, IContentApiService contentApiService)
    {
        _httpClient = httpClient;
        _sessionStorage = sessionStorage;
        _contentApiService = contentApiService;
    }

    public string? GetToken() => _currentSession?.Token;

    public async Task InitializeAsync()
    {
        if (_isInitialized) return;
        
        try
        {
            var result = await _sessionStorage.GetAsync<UserSession>(SessionKey);
            if (result.Success && result.Value != null)
            {
                _currentSession = result.Value;
                SetAuthHeaders(_currentSession.Token);
            }
        }
        catch
        {
            // Ignore errors during prerendering
        }
        
        _isInitialized = true;
    }

    private void SetAuthHeaders(string? token)
    {
        if (string.IsNullOrEmpty(token))
        {
            _httpClient.DefaultRequestHeaders.Authorization = null;
            _contentApiService.SetAuthToken(null);
        }
        else
        {
            _httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            _contentApiService.SetAuthToken(token);
        }
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
                    UserName = result.UserLogin ?? result.UserName ?? username,
                    Token = result.AccessToken,
                    Role = result.UserRole ?? result.Role ?? ""
                };
                
                SetAuthHeaders(result.AccessToken);
                
                try
                {
                    await _sessionStorage.SetAsync(SessionKey, _currentSession);
                }
                catch
                {
                    // Ignore during prerendering
                }
                
                return true;
            }
        }
        return false;
    }

    public async Task<bool> RegisterAsync(string username, string email, string password, string? inviteCode)
    {
        var request = new 
        { 
            Login = username, 
            Password = password,
            InviteCode = inviteCode
        };
        var response = await _httpClient.PostAsJsonAsync("api/auth/register", request);
        return response.IsSuccessStatusCode;
    }

    public async Task LogoutAsync()
    {
        _currentSession = null;
        SetAuthHeaders(null);
        
        try
        {
            await _sessionStorage.DeleteAsync(SessionKey);
        }
        catch
        {
            // Ignore during prerendering
        }
    }

    public async Task<UserSession?> GetCurrentSessionAsync()
    {
        if (!_isInitialized)
        {
            await InitializeAsync();
        }
        return _currentSession;
    }

    private class LoginResponse
    {
        public int UserId { get; set; }
        public string? UserName { get; set; }
        public string? UserLogin { get; set; }
        public string AccessToken { get; set; } = "";
        public string? Role { get; set; }
        public string? UserRole { get; set; }
    }
}

public class UserSession
{
    public int UserId { get; set; }
    public string UserName { get; set; } = "";
    public string Token { get; set; } = "";
    public string Role { get; set; } = "";
}
