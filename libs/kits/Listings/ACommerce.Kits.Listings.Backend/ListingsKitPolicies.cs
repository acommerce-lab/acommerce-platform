using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.Kits.Listings.Backend;

/// <summary>
/// أسماء سياسات Authorization التي يَستهلكها <see cref="ListingsController"/>.
///
/// <para>الفكرة: الكيت يضع <c>[Authorize(Policy = "Listings.AuthenticatedWriter")]</c>
/// على الـ actions التي تتطلّب توثيقاً، التطبيق يقرّر ماذا تعني السياسة:</para>
/// <list type="bullet">
///   <item>تطبيق إنتاج — <c>RequireAuthenticatedUser()</c>.</item>
///   <item>تطبيق "تجربة مفتوحة" — <c>RequireAssertion(_ =&gt; true)</c>
///         فيمرّ كلّ شيء.</item>
///   <item>تطبيق ذو أدوار — <c>RequireRole("user", "admin")</c>.</item>
/// </list>
///
/// <para>الكيت يَكشف <see cref="AddListingsKitPolicies"/> الذي يُسجِّل
/// الافتراضيّات (<c>RequireAuthenticatedUser</c>). التطبيق يستطيع override
/// قبل أو بعد عبر <c>services.AddAuthorization(opts =&gt; opts.AddPolicy(...))</c>
/// — آخر تسجيل يفوز.</para>
/// </summary>
public static class ListingsKitPolicies
{
    /// <summary>
    /// السياسة على write paths في <see cref="ListingsController"/> —
    /// POST /my-listings، PATCH، DELETE، toggle. الافتراضيّ:
    /// <c>RequireAuthenticatedUser</c>. تطبيقات الـ trial تُلغيه.
    /// </summary>
    public const string AuthenticatedWriter = "Listings.AuthenticatedWriter";

    /// <summary>
    /// يُسجِّل سياسات الكيت بالافتراضيّات — يُستدعى من
    /// <see cref="ListingsKitExtensions.AddListingsKit"/> تلقائيّاً.
    /// </summary>
    public static IServiceCollection AddListingsKitPolicies(this IServiceCollection services)
    {
        services.AddAuthorization(opts =>
        {
            // AddPolicy سلوكه idempotent بالاسم — لو التطبيق أضافها قبلنا
            // فالـ AddAuthorization Configure يَدمج، نسخة التطبيق هي التي
            // تَستقرّ في النهاية لو تكرّرت الأسماء.
            opts.AddPolicy(AuthenticatedWriter, p => p.RequireAuthenticatedUser());
        });
        return services;
    }
}
