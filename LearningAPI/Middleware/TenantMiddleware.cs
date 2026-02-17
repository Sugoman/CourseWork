using LearningAPI.Services;
using LearningTrainerShared.Context;

namespace LearningAPI.Middleware;

/// <summary>
/// Устанавливает TenantUserId на ApiDbContext из claims текущего пользователя.
/// Это активирует EF Core Query Filters для multi-tenant изоляции.
/// </summary>
public class TenantMiddleware
{
    private readonly RequestDelegate _next;

    public TenantMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ApiDbContext dbContext, ITenantProvider tenantProvider)
    {
        dbContext.TenantUserId = tenantProvider.CurrentUserId;
        await _next(context);
    }
}
