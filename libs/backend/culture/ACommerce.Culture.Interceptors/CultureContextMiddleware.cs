using ACommerce.Culture.Abstractions;
using ACommerce.Culture.Defaults;
using Microsoft.AspNetCore.Http;

namespace ACommerce.Culture.Interceptors;

/// <summary>
/// يقرأ المنطقة الزمنية + اللغة + نظام الأرقام من هيدرات الطلب
/// ويكتبها في MutableCultureContext الـ Scoped لهذا الطلب.
///
/// الهيدرات المتوقّعة (يرسلها الفرونت):
///   X-Timezone:  Asia/Riyadh
///   Accept-Language: ar,en;q=0.8
///   X-Numeral-System: arabic-indic | latin | persian
/// </summary>
public sealed class CultureContextMiddleware
{
    private readonly RequestDelegate _next;
    public CultureContextMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext ctx, ICultureContext culture)
    {
        if (culture is MutableCultureContext mut)
        {
            var tz = ctx.Request.Headers["X-Timezone"].ToString();
            var lang = ctx.Request.Headers["Accept-Language"].ToString();
            var primaryLang = lang.Split(',', ';').FirstOrDefault()?.Trim();
            var numerals = ctx.Request.Headers["X-Numeral-System"].ToString();
            mut.Set(
                tz:       string.IsNullOrWhiteSpace(tz) ? null : tz,
                lang:     string.IsNullOrWhiteSpace(primaryLang) ? null : primaryLang,
                numerals: string.IsNullOrWhiteSpace(numerals) ? null : numerals);
        }
        await _next(ctx);
    }
}
