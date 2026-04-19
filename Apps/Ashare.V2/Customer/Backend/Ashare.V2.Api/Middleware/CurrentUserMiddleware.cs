using Ashare.V2.Api.Services;

namespace Ashare.V2.Api.Middleware;

/// <summary>
/// Demo AuthN: يقرأ <c>X-User-Id</c> header (إن وُجد) ويضع المعرّف في
/// <c>HttpContext.Items["user_id"]</c>. إن لم يُرسَل المتصفّح الـ header،
/// يستعمل <c>AshareV2Seed.CurrentUserId</c> كقيمة افتراضيّة.
///
/// في بيئة الإنتاج: يُستبدَل بـ JWT middleware يقرأ claim "sub".
/// هذا الترتيب يجعل العمليّات تعتمد على معرّف مُمرَّر (لا ثابت)
/// فنكون جاهزين لأيّ AuthN بلا لمس أيّ controller.
/// </summary>
public sealed class CurrentUserMiddleware
{
    private readonly RequestDelegate _next;
    public CurrentUserMiddleware(RequestDelegate next) => _next = next;

    public async Task Invoke(HttpContext ctx)
    {
        var userId = ctx.Request.Headers.TryGetValue("X-User-Id", out var h) && !string.IsNullOrEmpty(h)
            ? h.ToString()
            : AshareV2Seed.CurrentUserId;
        ctx.Items["user_id"] = userId;
        await _next(ctx);
    }
}

public static class CurrentUserMiddlewareExtensions
{
    public static IApplicationBuilder UseCurrentUser(this IApplicationBuilder app)
        => app.UseMiddleware<CurrentUserMiddleware>();
}
