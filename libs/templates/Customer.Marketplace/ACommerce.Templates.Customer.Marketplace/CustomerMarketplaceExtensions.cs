using ACommerce.ClientHost;
using ACommerce.ClientHost.Auth;
using ACommerce.ClientHost.KitApi;
using ACommerce.Kits.Auth.Frontend.Customer.Stores;
using ACommerce.Kits.Chat.Frontend.Customer.Stores;
using ACommerce.Kits.Favorites.Frontend.Customer.Stores;
using ACommerce.Kits.Listings.Frontend.Customer.Stores;
using ACommerce.Kits.Notifications.Frontend.Customer.Stores;
using ACommerce.Kits.Profiles.Frontend.Customer.Stores;
using ACommerce.Kits.Subscriptions.Frontend.Customer.Stores;
using ACommerce.Kits.Support.Frontend.Customer.Stores;
using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.Templates.Customer.Marketplace;

/// <summary>
/// نُقطة دَخول قالَب Customer Marketplace — يُسَجِّل ٨ ApiClients + ٨
/// Default Stores + AddClientAuth. routes الافتراضيّة لا تُسَجَّل في هذا
/// الإصدار: التَطبيقات لا تَزال تَستخدِم <c>@page</c>-routing مَع pages
/// مُرَكَّبة من <c>libs/templates/Marketplace</c> (AcListingExplorePage،
/// AcCreateListingPage…). التَّحويل الكامِل لِـ KitPageRegistry-routing
/// يَأتي عِندما تَنفصِل صَفحات Marketplace عَن V1 services.
/// </summary>
public static class CustomerMarketplaceExtensions
{
    public static IServiceCollection AddTemplate_Customer_Marketplace(
        this IServiceCollection services,
        Action<CustomerMarketplaceOptions> configure)
    {
        var opts = new CustomerMarketplaceOptions();
        configure(opts);

        if (opts.RegisterAuth)
        {
            services.AddClientAuth(o =>
            {
                o.HttpClientName = opts.HttpClientName;
                o.StorageKey     = opts.StorageKey;
                o.Scheme         = opts.Scheme;
            });
        }

        services.AddKitApiPipeline(sp => sp.GetRequiredService<AuthenticatedHttpClient>().Client);
        services.AddScoped<IAuthApiClient,          HttpAuthApiClient>();
        services.AddScoped<IListingsApiClient,      HttpListingsApiClient>();
        services.AddScoped<IChatApiClient,          HttpChatApiClient>();
        services.AddScoped<INotificationsApiClient, HttpNotificationsApiClient>();
        services.AddScoped<IProfileApiClient,       HttpProfileApiClient>();
        services.AddScoped<ISubscriptionsApiClient, HttpSubscriptionsApiClient>();
        services.AddScoped<ISupportApiClient,       HttpSupportApiClient>();
        services.AddScoped<IFavoritesApiClient,     HttpFavoritesApiClient>();

        services.AddACommerceClientHost(client =>
        {
            client.UseUrlAllowlist(a =>
            {
                foreach (var host in opts.UrlAllowlist) a.Add(host);
            });
            client.AddDomainBindings(b => b
                .Use<IAuthStore,          DefaultAuthStore>()
                .Use<IListingsStore,      DefaultListingsStore>()
                .Use<IChatStore,          DefaultChatStore>()
                .Use<INotificationsStore, DefaultNotificationsStore>()
                .Use<IProfileStore,       DefaultProfileStore>()
                .Use<ISubscriptionsStore, DefaultSubscriptionsStore>()
                .Use<ISupportStore,       DefaultSupportStore>()
                .Use<IFavoritesStore,     DefaultFavoritesStore>());
        });

        return services;
    }
}

public sealed class CustomerMarketplaceOptions
{
    public string HttpClientName { get; set; } = "";
    public string StorageKey     { get; set; } = "";
    public string Scheme         { get; set; } = "";
    public bool   RegisterAuth   { get; set; } = true;
    public List<string> UrlAllowlist { get; } = new();
}
