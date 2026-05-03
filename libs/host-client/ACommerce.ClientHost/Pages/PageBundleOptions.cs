namespace ACommerce.ClientHost.Pages;

/// <summary>
/// خيارات لكلّ <see cref="IPageBundle"/> عند تسجيله. التطبيق يُعيد تسمية route
/// أو يُخفي صفحة. الـ overrides تُطبَّق وقت بناء <see cref="KitPageRegistry"/>.
/// </summary>
public sealed class PageBundleOptions
{
    private readonly Dictionary<string, string> _routeOverrides = new(StringComparer.Ordinal);
    private readonly HashSet<string> _hidden = new(StringComparer.Ordinal);

    /// <summary>غيّر مسار صفحة — مثلاً <c>"listings.index"</c> → <c>"/properties"</c>.</summary>
    public PageBundleOptions Rename(string pageId, string newRoute)
    {
        _routeOverrides[pageId] = newRoute;
        return this;
    }

    /// <summary>أزل صفحة من الـ routing تماماً (لا تُسَجَّل).</summary>
    public PageBundleOptions Hide(string pageId)
    {
        _hidden.Add(pageId);
        return this;
    }

    internal bool IsHidden(string pageId) => _hidden.Contains(pageId);

    internal string ResolveRoute(string pageId, string defaultRoute) =>
        _routeOverrides.TryGetValue(pageId, out var r) ? r : defaultRoute;
}
