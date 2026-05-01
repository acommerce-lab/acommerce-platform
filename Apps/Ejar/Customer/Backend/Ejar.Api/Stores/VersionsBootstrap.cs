using ACommerce.Kits.Versions.Backend;
using ACommerce.Kits.Versions.Operations;
using ACommerce.SharedKernel.Repositories.Interfaces;

namespace Ejar.Api.Stores;

/// <summary>
/// يقرأ <c>Versions:Latest:{platform}</c> من <c>appsettings.json</c> ويُسجّل كلّ
/// قيمة كـ <see cref="VersionStatus.Latest"/> في <see cref="IVersionStore"/>.
/// يُستدعى مرّة واحدة عند بدء التشغيل بعد الترحيلات والبذور. منطق
/// auto-demote في <c>EjarVersionStore.UpsertAsync</c> يُخفّض أيّ Latest سابق
/// في نفس المنصّة إلى Active تلقائياً، فالنشر يكفي ليُصبح الإصدار الجديد
/// هو الـ Latest بلا الحاجة لاستدعاء admin endpoint يدوياً.
///
/// <para>مثال للـ <c>appsettings.json</c>:
/// <code>
/// "Versions": {
///   "Latest": {
///     "wasm":   "2026.04.29.1",
///     "web":    "2026.04.29.1",
///     "mobile": "1.0.0",
///     "admin":  "1.0.0"
///   }
/// }
/// </code></para>
/// </summary>
public static class VersionsBootstrap
{
    public static async Task PromoteFromConfigAsync(
        IServiceProvider sp, IConfiguration config, CancellationToken ct = default)
    {
        var section = config.GetSection("Versions:Latest");
        if (!section.Exists()) return;

        var store = sp.GetService<IVersionStore>();
        if (store is null) return;

        foreach (var entry in section.GetChildren())
        {
            var platform = entry.Key;
            var version  = entry.Value?.Trim();
            if (string.IsNullOrWhiteSpace(platform) || string.IsNullOrWhiteSpace(version))
                continue;

            await store.UpsertAsync(
                new AppVersion(platform, version!, VersionStatus.Latest),
                ct);
        }

        // (F6) UpsertAsync الآن tracker-only — البـوّابة الإداريّة تحفظ عبر
        // OperationBuilder.SaveAtEnd. هذا startup path لا يمرّ بـ OpEngine،
        // فنحفظ مباشرةً عبر IUnitOfWork (نفس الـ scoped DbContext).
        var uow = sp.GetService<IUnitOfWork>();
        if (uow is not null) await uow.SaveChangesAsync(ct);
    }
}
