using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.ClientHost.Pages;

/// <summary>
/// builder الصفحات — يَجمع <see cref="IPageBundle"/> مسجَّلين، ويُمرّر options
/// لكلّ bundle. يُعبَّأ في <see cref="KitPageRegistry"/> singleton.
/// </summary>
public sealed class KitPageBuilder
{
    public IServiceCollection Services { get; }
    private readonly KitPageRegistry _registry;

    public KitPageBuilder(IServiceCollection services, KitPageRegistry registry)
    {
        Services = services;
        _registry = registry;
    }

    /// <summary>أضِف bundle مع خيارات اختياريّة (<c>Rename</c>/<c>Hide</c>).</summary>
    public KitPageBuilder Add<TBundle>(Action<PageBundleOptions>? configure = null)
        where TBundle : IPageBundle, new()
    {
        var bundle = new TBundle();
        var opts = new PageBundleOptions();
        configure?.Invoke(opts);
        _registry.Register(bundle, opts);
        return this;
    }
}

/// <summary>
/// builder صفحات التطبيق الخاصّة — صفحات لا تَنتمي لكيت (about, terms…).
/// </summary>
public sealed class AppPageBuilder
{
    public IServiceCollection Services { get; }
    private readonly KitPageRegistry _registry;

    public AppPageBuilder(IServiceCollection services, KitPageRegistry registry)
    {
        Services = services;
        _registry = registry;
    }

    /// <summary>سجّل صفحة تطبيق على route ثابت.</summary>
    public AppPageBuilder Add(string route, Type component, bool requiresAuth = false)
    {
        _registry.RegisterApp(route, component, requiresAuth);
        return this;
    }
}
