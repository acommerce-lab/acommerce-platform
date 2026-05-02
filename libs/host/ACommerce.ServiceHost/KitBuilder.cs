using ACommerce.Compositions.Core;
using ACommerce.Kits.Auth;
using ACommerce.Kits.Auth.Backend;
using ACommerce.Kits.Auth.Operations;
using ACommerce.Kits.Auth.TwoFactor.AsAuth;
using ACommerce.Kits.Chat;
using ACommerce.Kits.Chat.Backend;
using ACommerce.Kits.Discovery.Backend;
using ACommerce.Favorites.Backend;
using ACommerce.Kits.Listings.Backend;
using ACommerce.Kits.Notifications.Backend;
using ACommerce.Kits.Profiles.Backend;
using ACommerce.Kits.Reports.Backend;
using ACommerce.Kits.Subscriptions.Backend;
using ACommerce.Kits.Support.Backend;
using ACommerce.Kits.Versions.Backend;
using ACommerce.Authentication.TwoFactor.Providers.Sms.Mock.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.ServiceHost;

/// <summary>
/// builder الكيتس — يحوّل <c>kits.AddXxx&lt;TStore&gt;()</c> إلى
/// <c>services.AddXxxKit&lt;TStore&gt;()</c> داخليّاً. يجمع كلّ تسجيلات
/// الكيتس في كتلة واحدة قابلة للقراءة.
/// </summary>
public sealed class KitBuilder
{
    public IServiceCollection Services { get; }
    public IConfiguration Configuration { get; }

    public KitBuilder(IServiceCollection services, IConfiguration configuration)
    {
        Services = services; Configuration = configuration;
    }

    // ── Auth + 2FA ───────────────────────────────────────────────────────
    public KitBuilder AddAuth<TStore>(AuthKitJwtConfig jwt) where TStore : class, IAuthUserStore
    {
        Services.AddAuthKit<TStore>(jwt);
        return this;
    }

    /// <summary>
    /// تسجيل Auth + JWT من section في <c>appsettings.json</c>:
    /// <code>
    /// "JWT": { "SecretKey": "…", "Issuer": "…", "Audience": "…", "Role": "user", "PartyKind": "User", "AccessTokenLifetimeDays": 30 }
    /// </code>
    /// </summary>
    public KitBuilder AddAuth<TStore>(string sectionName = "JWT") where TStore : class, IAuthUserStore
    {
        var s = Configuration.GetSection(sectionName);
        var jwt = new AuthKitJwtConfig(
            Secret:                  s["SecretKey"] ?? throw new InvalidOperationException($"{sectionName}:SecretKey is required"),
            Issuer:                  s["Issuer"]    ?? "",
            Audience:                s["Audience"]  ?? "",
            Role:                    s["Role"]      ?? "user",
            PartyKind:               s["PartyKind"] ?? "User",
            AccessTokenLifetimeDays: s.GetValue<int?>("AccessTokenLifetimeDays") ?? 30
        );
        return AddAuth<TStore>(jwt);
    }

    public KitBuilder AddTwoFactorMockSms()
    {
        Services.AddMockSmsTwoFactor();
        Services.AddTwoFactorAsAuth();
        return this;
    }

    // ── Chat ──────────────────────────────────────────────────────────────
    public KitBuilder AddChat<TStore>() where TStore : class, IChatStore
    {
        Services.AddChatKit<TStore>();
        // AddChatKit يسجّل IChatStore Singleton؛ تطبيقات بـ DbContext-bound store
        // تحتاج Scoped. نُعيد تسجيله Scoped (descriptor الأخير يفوز).
        Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions
            .RemoveAll<IChatStore>(Services);
        Services.AddScoped<IChatStore, TStore>();
        return this;
    }

    public KitBuilder AddChatPresenceProbe<TProbe>() where TProbe : class, IPresenceProbe
    {
        Services.AddScoped<IPresenceProbe, TProbe>();
        return this;
    }

    // ── Discovery ─────────────────────────────────────────────────────────
    public KitBuilder AddDiscovery()
    {
        Services.AddDiscoveryKit();
        return this;
    }

    // ── Favorites ─────────────────────────────────────────────────────────
    public KitBuilder AddFavorites()
    {
        Services.AddFavoritesKit();
        return this;
    }

    // ── Listings ──────────────────────────────────────────────────────────
    public KitBuilder AddListings<TStore>(Action<ListingsKitOptions>? configure = null)
        where TStore : class, IListingStore
    {
        Services.AddListingsKit<TStore>(configure);
        return this;
    }

    // ── Notifications ─────────────────────────────────────────────────────
    public KitBuilder AddNotifications<TStore>() where TStore : class, INotificationStore
    {
        Services.AddNotificationsKit<TStore>();
        return this;
    }

    // ── Profiles ──────────────────────────────────────────────────────────
    public KitBuilder AddProfiles<TStore>() where TStore : class, IProfileStore
    {
        Services.AddProfilesKit<TStore>();
        return this;
    }

    // ── Reports ───────────────────────────────────────────────────────────
    public KitBuilder AddReports<TStore>(Action<ReportsKitOptions>? configure = null)
        where TStore : class, ACommerce.Kits.Reports.Operations.IReportStore
    {
        Services.AddReportsKit<TStore>(configure);
        return this;
    }

    // ── Subscriptions ─────────────────────────────────────────────────────
    public KitBuilder AddSubscriptions<TSub, TPlan, TInv>(Action<SubscriptionsKitOptions>? configure = null)
        where TSub  : class, ISubscriptionStore
        where TPlan : class, IPlanStore
        where TInv  : class, IInvoiceStore
    {
        Services.AddSubscriptionsKit<TSub, TPlan, TInv>(configure);
        return this;
    }

    // ── Support ───────────────────────────────────────────────────────────
    public KitBuilder AddSupport<TStore>(Action<SupportKitOptions>? configure = null)
        where TStore : class, ACommerce.Kits.Support.Operations.ISupportStore
    {
        Services.AddSupportKit<TStore>(configure);
        return this;
    }

    // ── Versions ──────────────────────────────────────────────────────────
    public KitBuilder AddVersions<TStore>() where TStore : class, IVersionStore
    {
        Services.AddVersionsKit<TStore>();
        return this;
    }
}

/// <summary>
/// builder التراكيب — يَجمع <c>AddComposition</c> في كتلة واحدة.
/// </summary>
public sealed class CompositionBuilder
{
    public IServiceCollection Services { get; }
    public CompositionBuilder(IServiceCollection services) => Services = services;

    public CompositionBuilder Add<TComposition>() where TComposition : ICompositionDescriptor, new()
    {
        Services.AddComposition<TComposition>();
        return this;
    }
}
