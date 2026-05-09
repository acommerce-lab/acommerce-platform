using ACommerce.Kits.Support.Operations;
using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.Kits.Support.Backend;

/// <summary>
/// تسجيل Support kit في DI. التطبيق يحقن <typeparamref name="TStore"/> الذي
/// يُنفِّذ <see cref="ISupportStore"/>.
///
/// <para>Support kit الآن مَعزول تماماً عن Chat kit — لا يَتَطلّب
/// <c>IChatStore</c>. كلّ تذكرة + رسائلها في تخزين Support الخاصّ.</para>
/// </summary>
public static class SupportKitExtensions
{
    public static IServiceCollection AddSupportKit<TStore>(
        this IServiceCollection services,
        Action<SupportKitOptions>? configure = null)
        where TStore : class, ISupportStore
    {
        var options = new SupportKitOptions();
        configure?.Invoke(options);
        services.AddSingleton(options);
        services.AddScoped<ISupportStore, TStore>();
        services.AddControllers().AddApplicationPart(typeof(SupportController).Assembly);
        services.AddSupportKitPolicies();
        return services;
    }
}
