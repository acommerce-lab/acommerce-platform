namespace ACommerce.ClientHost.Security;

/// <summary>
/// قائمة الـ hosts المسموح بها لروابط الصور/الـ embeds الخارجيّة. templates
/// التي تَربط <c>src</c>/<c>href</c> لقيم قادمة من الخادم تَستدعي
/// <see cref="IsAllowed"/> أوّلاً — إن لم يَكن المُضيف في القائمة تُعرَض صورة
/// fallback أو لا تُعرَض إطلاقاً. حماية ضدّ data-exfil pixels و mixed-content.
/// </summary>
public interface IUrlAllowlist
{
    /// <summary>صحّ لو المُضيف الموجود في <paramref name="absoluteUrl"/> مسموح.</summary>
    bool IsAllowed(string? absoluteUrl);
}

/// <summary>التنفيذ الافتراضيّ: قائمة hosts ثابتة + relative URLs مسموحة دائماً.</summary>
public sealed class StaticUrlAllowlist : IUrlAllowlist
{
    private readonly HashSet<string> _hosts;

    public StaticUrlAllowlist(IEnumerable<string> allowedHosts) =>
        _hosts = new HashSet<string>(allowedHosts, StringComparer.OrdinalIgnoreCase);

    public bool IsAllowed(string? absoluteUrl)
    {
        if (string.IsNullOrWhiteSpace(absoluteUrl)) return false;
        if (!Uri.TryCreate(absoluteUrl, UriKind.Absolute, out var uri))
            return absoluteUrl.StartsWith('/');   // relative URL داخل التطبيق — مسموح
        if (uri.Scheme is not ("http" or "https")) return false;
        return _hosts.Contains(uri.Host);
    }
}

/// <summary>builder لـ <see cref="StaticUrlAllowlist"/>.</summary>
public sealed class UrlAllowlistBuilder
{
    private readonly List<string> _hosts = new();
    public UrlAllowlistBuilder Add(params string[] hosts) { _hosts.AddRange(hosts); return this; }
    internal IUrlAllowlist Build() => new StaticUrlAllowlist(_hosts);
}
