using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.Kits.Versions.Templates;

public static class VersionsTemplatesExtensions
{
    /// <summary>
    /// يسجّل خدمات بوّابة الإصدار للواجهة الأماميّة:
    /// <list type="bullet">
    ///   <item><see cref="AppVersionInfo"/> singleton (يجب أن يكون مُسجَّلاً قبل هذا).</item>
    ///   <item><see cref="AppVersionHeadersHandler"/> (DelegatingHandler).</item>
    ///   <item><see cref="VersionState"/> singleton.</item>
    /// </list>
    /// لتشغيل الـ guard على الـ HttpClient، استخدم:
    /// <code>
    ///   builder.Services.AddHttpClient("ejar", c =&gt; c.BaseAddress = ...)
    ///                   .AddHttpMessageHandler&lt;AppVersionHeadersHandler&gt;();
    /// </code>
    /// </summary>
    public static IServiceCollection AddVersionsTemplates(
        this IServiceCollection services,
        string httpClientName)
    {
        services.AddTransient<AppVersionHeadersHandler>();
        services.AddSingleton(new VersionStateOptions { HttpClientName = httpClientName });
        services.AddSingleton<VersionState>();
        return services;
    }
}
