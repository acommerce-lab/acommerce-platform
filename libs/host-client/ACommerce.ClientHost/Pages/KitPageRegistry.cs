using System.Reflection;

namespace ACommerce.ClientHost.Pages;

/// <summary>
/// السجلّ المركزيّ لكلّ صفحات الـ kits + صفحات التطبيق المسجَّلة عبر
/// <c>AddKitPages</c>/<c>AddAppPages</c>. يُحقَن singleton ويَستعمله
/// <c>KitPageRouter</c> لحلّ URL → component وقت التشغيل.
///
/// <para>صفحات الكيت ليس لها <c>@page</c> directive — الـ Router لا يَكتشفها
/// عبر <c>AdditionalAssemblies</c>. بدلاً من ذلك يَستعمل
/// <see cref="ResolveAsync"/> الذي يطابق المسار النصّيّ مع
/// <see cref="ResolvedPage.Route"/> (يَدعم patterns بسيطة مثل
/// <c>"/properties/{id}"</c>).</para>
/// </summary>
public sealed class KitPageRegistry
{
    private readonly List<ResolvedPage> _pages = new();

    /// <summary>كلّ الصفحات النهائيّة بعد تطبيق الـ overrides + الـ hides.</summary>
    public IReadOnlyList<ResolvedPage> Pages => _pages;

    /// <summary>الـ assemblies التي تَحوي components الصفحات — للـ Router.</summary>
    public IReadOnlyList<Assembly> PageAssemblies =>
        _pages.Select(p => p.Component.Assembly).Distinct().ToList();

    internal void Register(IPageBundle bundle, PageBundleOptions opts)
    {
        foreach (var p in bundle.Pages)
        {
            if (opts.IsHidden(p.PageId)) continue;
            var route = opts.ResolveRoute(p.PageId, p.Route);
            _pages.Add(new ResolvedPage(p.PageId, p.Component, route, p.RequiresAuth, bundle.BundleId));
        }
    }

    internal void RegisterApp(string route, Type component, bool requiresAuth = false) =>
        _pages.Add(new ResolvedPage(
            PageId:       $"app.{route}",
            Component:    component,
            Route:        route,
            RequiresAuth: requiresAuth,
            BundleId:     "app"));

    /// <summary>
    /// يَطابق <paramref name="urlPath"/> مع صفحة مسجَّلة. يَدعم patterns بسيطة
    /// تَحوي <c>{name}</c> placeholders. يُعيد <c>null</c> لو لم تُوجَد مطابقة.
    /// </summary>
    public PageMatch? Resolve(string urlPath)
    {
        urlPath = NormalizePath(urlPath);
        foreach (var page in _pages)
        {
            var routeParams = TryMatch(page.Route, urlPath);
            if (routeParams is not null)
                return new PageMatch(page, routeParams);
        }
        return null;
    }

    private static string NormalizePath(string path)
    {
        var q = path.IndexOf('?');
        if (q >= 0) path = path[..q];
        return path.Length == 0 || path[0] != '/' ? "/" + path : path;
    }

    private static Dictionary<string, string>? TryMatch(string pattern, string path)
    {
        var pSegs    = pattern.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        var pathSegs = path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (pSegs.Length != pathSegs.Length) return null;

        var captures = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < pSegs.Length; i++)
        {
            var ps = pSegs[i];
            if (ps.Length > 1 && ps[0] == '{' && ps[^1] == '}')
                captures[ps[1..^1]] = Uri.UnescapeDataString(pathSegs[i]);
            else if (!string.Equals(ps, pathSegs[i], StringComparison.OrdinalIgnoreCase))
                return null;
        }
        return captures;
    }
}

/// <summary>
/// صفحة نهائيّة مسجَّلة — بعد تطبيق الـ overrides + الـ hides من
/// <see cref="PageBundleOptions"/>.
/// </summary>
public sealed record ResolvedPage(
    string PageId,
    Type Component,
    string Route,
    bool RequiresAuth,
    string BundleId);

/// <summary>نتيجة <see cref="KitPageRegistry.Resolve(string)"/>.</summary>
public sealed record PageMatch(ResolvedPage Page, IReadOnlyDictionary<string, string> RouteParameters);
