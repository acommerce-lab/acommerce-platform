namespace Ashare.V2.Api.Middleware;

/// <summary>
/// يقرأ رؤوس ثقافة المستخدم التي يحقنها <c>CultureHeadersHandler</c>
/// على العميل:
///   <c>Accept-Language</c> → <c>HttpContext.Items["culture.language"]</c>
///   <c>X-User-Timezone</c> → <c>HttpContext.Items["culture.timezone"]</c>
///   <c>X-User-Currency</c> → <c>HttpContext.Items["culture.currency"]</c>
///
/// المتحكّمات تستعمل هذه القيم عند الحاجة (تصحيح وارد من تواريخ العميل،
/// ترجمة قبل التخزين، تحويل عملات عند الحدود، إلخ).
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
        ctx.Items[CurrencyKey] = HeaderOr(ctx, "X-User-Currency", "SAR");
        await _next(ctx);
    }

    private static string HeaderOr(HttpContext ctx, string key, string fallback)
    {
        if (!ctx.Request.Headers.TryGetValue(key, out var v) || string.IsNullOrEmpty(v))
            return fallback;
        // Accept-Language قد يحمل سلسلة "ar,en;q=0.8" — نأخذ أوّل لغة.
        var raw = v.ToString();
        var ix = raw.IndexOfAny(new[] { ',', ';' });
        return ix > 0 ? raw[..ix].Trim() : raw.Trim();
    }
}

public static class CurrentCultureMiddlewareExtensions
{
    public static IApplicationBuilder UseCurrentCulture(this IApplicationBuilder app)
        => app.UseMiddleware<CurrentCultureMiddleware>();
}
