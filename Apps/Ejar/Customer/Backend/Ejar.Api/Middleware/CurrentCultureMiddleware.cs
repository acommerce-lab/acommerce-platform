namespace Ejar.Api.Middleware;

/// <summary>
/// يقرأ رؤوس الثقافة التي يحقنها <c>CultureHeadersHandler</c> على الواجهة:
///   Accept-Language  → HttpContext.Items["culture.language"]
///   X-User-Timezone  → HttpContext.Items["culture.timezone"]
///   X-User-Currency  → HttpContext.Items["culture.currency"]
/// </summary>
public sealed class CurrentCultureMiddleware
{
    public const string LanguageKey = "culture.language";
    public const string TimeZoneKey = "culture.timezone";
    public const string CurrencyKey = "culture.currency";

    private readonly RequestDelegate _next;
    public CurrentCultureMiddleware(RequestDelegate next) => _next = next;

    public async Task Invoke(HttpContext ctx)
    {
        ctx.Items[LanguageKey] = HeaderOr(ctx, "Accept-Language", "ar");
        ctx.Items[TimeZoneKey] = HeaderOr(ctx, "X-User-Timezone", "Asia/Riyadh");
        ctx.Items[CurrencyKey] = HeaderOr(ctx, "X-User-Currency",  "SAR");
        await _next(ctx);
    }

    private static string HeaderOr(HttpContext ctx, string key, string fallback)
    {
        if (!ctx.Request.Headers.TryGetValue(key, out var v) || string.IsNullOrEmpty(v))
            return fallback;
        var raw = v.ToString();
        var ix  = raw.IndexOfAny([',', ';']);
        return ix > 0 ? raw[..ix].Trim() : raw.Trim();
    }
}

public static class CurrentCultureMiddlewareExtensions
{
    public static IApplicationBuilder UseCurrentCulture(this IApplicationBuilder app)
        => app.UseMiddleware<CurrentCultureMiddleware>();
}
