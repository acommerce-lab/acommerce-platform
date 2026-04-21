using Ejar.Api.Services;

namespace Ejar.Api.Middleware;

/// <summary>
/// يقرأ user_id من JWT claim ويضعه في <c>HttpContext.Items["user_id"]</c>.
/// يرجع إلى <c>EjarSeed.CurrentUserId</c> إن لم يكن المستخدم مصادقاً.
/// </summary>
public sealed class CurrentUserMiddleware
{
    private readonly RequestDelegate _next;
    public CurrentUserMiddleware(RequestDelegate next) => _next = next;

    public async Task Invoke(HttpContext ctx)
    {
        var userId = ctx.User.FindFirst("user_id")?.Value
                     ?? ctx.User.FindFirst("sub")?.Value
                     ?? EjarSeed.CurrentUserId;
        ctx.Items["user_id"] = userId;
        await _next(ctx);
    }
}

public static class CurrentUserMiddlewareExtensions
{
    public static IApplicationBuilder UseCurrentUser(this IApplicationBuilder app)
        => app.UseMiddleware<CurrentUserMiddleware>();
}
