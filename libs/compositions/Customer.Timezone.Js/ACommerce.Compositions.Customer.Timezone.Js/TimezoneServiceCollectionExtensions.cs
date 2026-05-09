using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.Compositions.Customer.Timezone.Js;

public static class TimezoneServiceCollectionExtensions
{
    /// <summary>
    /// يُسَجِّل <see cref="JsTimezoneProvider"/> كَـ <see cref="ITimezoneProvider"/>.
    /// التَطبيق يَحتاج لِتَحميل JS module (انظر README): إضافة
    /// <c>&lt;script src="_content/ACommerce.Compositions.Customer.Timezone.Js/ac-tz.js"&gt;&lt;/script&gt;</c>
    /// قَبل Blazor boot.
    /// </summary>
    public static IServiceCollection AddJsTimezoneProvider(this IServiceCollection services)
    {
        services.AddScoped<ITimezoneProvider, JsTimezoneProvider>();
        return services;
    }
}
