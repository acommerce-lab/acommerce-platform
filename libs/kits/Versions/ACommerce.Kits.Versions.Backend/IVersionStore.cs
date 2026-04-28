using ACommerce.Kits.Versions.Operations;

namespace ACommerce.Kits.Versions.Backend;

/// <summary>
/// منفذ بيانات الإصدارات. التطبيق ينفّذها مقابل تخزينه (in-memory، EF، JSON …).
/// عمليّات القراءة (List/Get/GetLatest) مستخدَمة من المعترض ومن الـ controller.
/// عمليّات الكتابة من admin controller فقط.
/// </summary>
public interface IVersionStore
{
    Task<IReadOnlyList<AppVersion>> ListAsync(string? platform, CancellationToken ct);

    Task<AppVersion?> GetAsync(string platform, string version, CancellationToken ct);

    Task<AppVersion?> GetLatestAsync(string platform, CancellationToken ct);

    /// <summary>
    /// يضيف أو يحدّث (upsert) الإصدار. في حالة التحديث الحقول الاختياريّة null
    /// تعني "اترك القيمة كما هي" (يقرّر التطبيق دلالة null تماماً).
    /// </summary>
    Task<AppVersion> UpsertAsync(AppVersion version, CancellationToken ct);

    Task<bool> SetStatusAsync(string platform, string version,
        VersionStatus status, DateTime? sunsetAt, CancellationToken ct);

    Task<bool> DeleteAsync(string platform, string version, CancellationToken ct);
}
