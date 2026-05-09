namespace ACommerce.ClientHost.Pages;

/// <summary>
/// كلّ kit يُصدِّر <c>IPageBundle</c> واحد يَصِف صفحاته بدون <c>@page</c>.
/// التطبيق يُسجِّلها عبر <c>.AddKitPages(p => p.Add&lt;ListingsPageBundle&gt;())</c>،
/// و<see cref="KitPageRegistry"/> يَجمعها كلّها في جدول واحد يَستهلكه
/// <c>KitPageRouter</c> لحلّ URL → component وقتَ التشغيل.
/// </summary>
public interface IPageBundle
{
    /// <summary>اسم الـ bundle — يُستعمَل في رسائل الخطأ + للـ override.</summary>
    string BundleId { get; }

    /// <summary>صفحات الكيت الافتراضيّة. التطبيق قد يُعيد تسميتها أو يُخفيها.</summary>
    IEnumerable<KitPage> Pages { get; }
}

/// <summary>
/// وصف صفحة كيت واحدة.
/// <para><paramref name="PageId"/> اسم منطقيّ (مثلاً <c>"listings.index"</c>) —
/// يُستعمَل في الـ <see cref="PageBundleOptions.Rename"/> و
/// <see cref="PageBundleOptions.Hide"/>. لا يُعرَض للمستخدم.</para>
/// <para><paramref name="Component"/> مكوّن Blazor (typeof) — يُعرَض في
/// <see cref="HostedApp"/> عبر <c>RouteView</c>.</para>
/// <para><paramref name="Route"/> المسار الافتراضيّ بنمط ASP.NET routing
/// (<c>"/listings/{id}"</c>). يقبل overrides من التطبيق.</para>
/// <para><paramref name="RequiresAuth"/> لو true الـ router يُحوّل غير
/// المُصادَق إلى <c>auth.login</c>.</para>
/// </summary>
public sealed record KitPage(
    string PageId,
    Type Component,
    string Route,
    bool RequiresAuth = false);
