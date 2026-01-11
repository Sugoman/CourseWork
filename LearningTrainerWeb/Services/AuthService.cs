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
}

public class AuthService : IAuthService
{
    private readonly HttpClient _httpClient;
    private UserSession? _currentSession;

    public AuthService(HttpClient httpClient)
    {
        _httpClient = httpClient;
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
                    UserName = result.UserName,
                    Token = result.AccessToken,
                    Role = result.Role
                };
                _httpClient.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", result.AccessToken);
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

    public Task LogoutAsync()
    {
        _currentSession = null;
        _httpClient.DefaultRequestHeaders.Authorization = null;
        return Task.CompletedTask;
    }

    public Task<UserSession?> GetCurrentSessionAsync()
    {
        return Task.FromResult(_currentSession);
    }

    private class LoginResponse
    {
        public int UserId { get; set; }
        public string UserName { get; set; } = "";
        public string AccessToken { get; set; } = "";
        public string Role { get; set; } = "";
    }
}

public class UserSession
{
    public int UserId { get; set; }
    public string UserName { get; set; } = "";
    public string Token { get; set; } = "";
    public string Role { get; set; } = "";
}
