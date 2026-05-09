using ACommerce.Kits.Versions.Operations;

namespace ACommerce.Kits.Versions.Backend;

/// <summary>
/// تطبيق افتراضيّ لـ <see cref="IAppVersionGate"/> يعتمد كليّاً على
/// <see cref="IVersionStore"/>. يكفي للتطبيقات التي لا تحتاج cache مخصّص أو منطقاً
/// مركّباً. التطبيق يستطيع تجاوزه واستخدام تطبيق خاصّ به (مع cache أو منطق إضافيّ).
///
/// <para>سياسة الإصدار غير المعروف قابلة للتشكيل عبر <see cref="VersionGateOptions"/>:
/// الافتراضيّ <see cref="UnknownVersionPolicy.Lenient"/> — نقبل الطلبات بإصدارات
/// غير مسجَّلة (نعاملها كـ Active). يفسّر هذا "ابدأ مرناً، شدّد لاحقاً": عند الإطلاق،
/// لا نعرف أيّ إصدارات في الميدان، فلا نريد حجب المستخدمين عشوائياً. كلّما عرفنا
/// إصداراً قديماً سجّلناه بحالة مناسبة (Deprecated/Unsupported)، وعند نضوج النظام
/// نُبدّل إلى <see cref="UnknownVersionPolicy.Strict"/>.</para>
/// </summary>
public sealed class StoreBackedAppVersionGate : IAppVersionGate
{
    private readonly IVersionStore _store;
    private readonly VersionGateOptions _options;

    public StoreBackedAppVersionGate(IVersionStore store, VersionGateOptions? options = null)
    {
        _store   = store;
        _options = options ?? new VersionGateOptions();
    }

    public async Task<VersionCheckResult> CheckAsync(string platform, string version, CancellationToken ct)
    {
        var entry  = await _store.GetAsync(platform, version, ct);
        var latest = await _store.GetLatestAsync(platform, ct);

        if (entry is null)
        {
            // إصدار غير معروف — السياسة تقرّر:
            //   Lenient (الافتراضيّ) → اقبله كـ Active، وأَعلِم العميل بوجود أحدث لو وُجد.
            //   Strict               → احجبه كـ Unsupported.
            var status = _options.UnknownVersionPolicy == UnknownVersionPolicy.Strict
                ? VersionStatus.Unsupported
                : VersionStatus.Active;

            return new VersionCheckResult(
                Platform:    platform,
                Version:     version,
                Status:      status,
                Latest:      latest?.Version,
                SunsetAt:    null,
                Notes:       null,
                DownloadUrl: latest?.DownloadUrl);
        }

        return new VersionCheckResult(
            Platform:    platform,
            Version:     version,
            Status:      entry.Status,
            Latest:      latest?.Version,
            SunsetAt:    entry.SunsetAt,
            Notes:       entry.Notes,
            DownloadUrl: latest?.DownloadUrl ?? entry.DownloadUrl);
    }
}
