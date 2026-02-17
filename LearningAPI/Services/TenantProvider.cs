namespace LearningAPI.Services;

/// <summary>
/// Поставщик текущего userId для EF Core Query Filters (multi-tenant изоляция).
/// Извлекает userId из HttpContext.User claims.
/// </summary>
public interface ITenantProvider
{
    int? CurrentUserId { get; }
}

public class HttpContextTenantProvider : ITenantProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextTenantProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public int? CurrentUserId
    {
        get
        {
            var claim = _httpContextAccessor.HttpContext?.User?
                .FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            return int.TryParse(claim, out var userId) ? userId : null;
        }
    }
}
