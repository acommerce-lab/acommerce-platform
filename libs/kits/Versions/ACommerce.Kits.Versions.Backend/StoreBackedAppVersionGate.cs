using ACommerce.Kits.Versions.Operations;

namespace ACommerce.Kits.Versions.Backend;

/// <summary>
/// تطبيق افتراضيّ لـ <see cref="IAppVersionGate"/> يعتمد كليّاً على
/// <see cref="IVersionStore"/>. يكفي للتطبيقات التي لا تحتاج cache مخصّص أو منطقاً
/// مركّباً. التطبيق يستطيع تجاوزه واستخدام تطبيق خاصّ به (مع cache أو منطق إضافيّ).
/// </summary>
public sealed class StoreBackedAppVersionGate : IAppVersionGate
{
    private readonly IVersionStore _store;

    public StoreBackedAppVersionGate(IVersionStore store) => _store = store;

    public async Task<VersionCheckResult> CheckAsync(string platform, string version, CancellationToken ct)
    {
        var entry  = await _store.GetAsync(platform, version, ct);
        var latest = await _store.GetLatestAsync(platform, ct);

        // إذا الإصدار غير معروف للخادم — نعتبره Unsupported افتراضيّاً.
        // التطبيقات التي تريد سياسة "الافتراضي = Latest" تستطيع تجاوز IAppVersionGate.
        if (entry is null)
        {
            return new VersionCheckResult(
                Platform:    platform,
                Version:     version,
                Status:      VersionStatus.Unsupported,
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
