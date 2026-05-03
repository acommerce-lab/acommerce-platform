using ACommerce.ClientHost.Pages;
using ACommerce.ClientHost.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ACommerce.ClientHost;

/// <summary>
/// builder الـ client host — مُكافِئ لـ <c>ServiceHost</c> الخلفيّ. التطبيق
/// يُسلسِل التسجيلات في كتلة واحدة:
/// <code>
/// services.AddACommerceClientHost(client => client
///     .UseSanitizer&lt;DefaultRichTextSanitizer&gt;()
///     .UseUrlAllowlist(a => a.Add("cdn.example.com"))
///     .AddKitPages(p => p.Add&lt;ListingsPageBundle&gt;())
///     .AddAppPages(p => p.Add("/about", typeof(AboutPage)))
///     .AddDomainBindings(b => b.Use&lt;IListingsStore, EjarListingsStore&gt;()));
/// </code>
/// </summary>
public sealed class ClientHostBuilder
{
    public IServiceCollection Services { get; }
    private readonly KitPageRegistry _registry = new();

    public ClientHostBuilder(IServiceCollection services)
    {
        Services = services;
        Services.TryAddSingleton(_registry);
        Services.TryAddSingleton<IRichTextSanitizer, DefaultRichTextSanitizer>();
        Services.TryAddSingleton<IUrlAllowlist>(_ => new StaticUrlAllowlist(Array.Empty<string>()));
    }

    /// <summary>تَسجيل sanitizer مخصّص للنصوص الغنيّة (يَستبدل الافتراضيّ).</summary>
    public ClientHostBuilder UseSanitizer<TSanitizer>()
        where TSanitizer : class, IRichTextSanitizer
    {
        Services.RemoveAll<IRichTextSanitizer>();
        Services.AddSingleton<IRichTextSanitizer, TSanitizer>();
        return this;
    }

    /// <summary>تَسجيل قائمة المُضيفين المسموح بها لـ external URLs.</summary>
    public ClientHostBuilder UseUrlAllowlist(Action<UrlAllowlistBuilder> configure)
    {
        var b = new UrlAllowlistBuilder();
        configure(b);
        Services.RemoveAll<IUrlAllowlist>();
        Services.AddSingleton(b.Build());
        return this;
    }

    /// <summary>صفحات الكيتس — كلّ kit يُصدِّر <c>IPageBundle</c>.</summary>
    public ClientHostBuilder AddKitPages(Action<KitPageBuilder> configure)
    {
        var b = new KitPageBuilder(Services, _registry);
        configure(b);
        return this;
    }

    /// <summary>صفحات خاصّة بالتطبيق (about, terms, help…).</summary>
    public ClientHostBuilder AddAppPages(Action<AppPageBuilder> configure)
    {
        var b = new AppPageBuilder(Services, _registry);
        configure(b);
        return this;
    }

    /// <summary>
    /// ربط واجهات الكيتس بتنفيذات التطبيق — <c>IListingsStore → EjarListingsStore</c>.
    /// كلّ pair يُسجَّل Scoped بحيث يَتشارك state داخل circuit/SPA session.
    /// </summary>
    public ClientHostBuilder AddDomainBindings(Action<DomainBindingsBuilder> configure)
    {
        var b = new DomainBindingsBuilder(Services);
        configure(b);
        return this;
    }
}

/// <summary>builder لربط واجهات الكيتس (<c>IXxxStore</c>) بتنفيذات التطبيق.</summary>
public sealed class DomainBindingsBuilder
{
    public IServiceCollection Services { get; }
    public DomainBindingsBuilder(IServiceCollection services) => Services = services;

    public DomainBindingsBuilder Use<TInterface, TImpl>()
        where TInterface : class
        where TImpl : class, TInterface
    {
        Services.AddScoped<TInterface, TImpl>();
        return this;
    }

    public DomainBindingsBuilder UseSingleton<TInterface, TImpl>()
        where TInterface : class
        where TImpl : class, TInterface
    {
        Services.AddSingleton<TInterface, TImpl>();
        return this;
    }
}
