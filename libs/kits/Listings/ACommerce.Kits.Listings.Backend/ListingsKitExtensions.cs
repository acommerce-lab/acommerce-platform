using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.Kits.Listings.Backend;

public static class ListingsKitExtensions
{
    /// <summary>
    /// يُسجِّل Listings kit: ListingsController + IListingStore.
    ///
    /// <para>متطلّبات: <c>OpEngine</c> مسجَّل.</para>
    /// </summary>
    public static IServiceCollection AddListingsKit<TStore>(
        this IServiceCollection services,
        Action<ListingsKitOptions>? configure = null)
        where TStore : class, IListingStore
    {
        var opts = new ListingsKitOptions();
        configure?.Invoke(opts);
        services.AddSingleton(opts);
        services.AddScoped<IListingStore, TStore>();
        services.AddControllers().AddApplicationPart(typeof(ListingsController).Assembly);
        // السياسات الافتراضيّة — التطبيق يَستطيع override بعد هذه الدالّة.
        services.AddListingsKitPolicies();
        return services;
    }
}
