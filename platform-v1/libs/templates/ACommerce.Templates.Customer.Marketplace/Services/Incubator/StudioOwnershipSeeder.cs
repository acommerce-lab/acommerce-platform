using ACommerce.Kit.Tenants;
using Marten;

namespace ACommerce.Templates.Customer.Marketplace.Services.Incubator;

/// <summary>
/// مُهَيِّئ مرَّة-واحِدَة: لَو وُجِدَ <see cref="StudioUser"/> واحِد على
/// الأَقَلّ ولَدَيه مَتاجِر بِلا <c>OwnerUserId</c>، يُسنِدها لِأَوَّل
/// مُستَخدِم (الأَقدَم). يَعمَل عِندَ بَدء التَطبيق + بَعد كُلّ
/// تَسجيل دُخول جَديد (لِيَلتَقِط أَيّ تَسجيل أَوَّل بَعد إطلاق الميزَة).
/// </summary>
public static class StudioOwnershipSeeder
{
    public static async Task RunAsync(IDocumentStore store, CancellationToken ct = default)
    {
        // أَوَّل StudioUser (الأَقدَم).
        await using var studioQs = store.QuerySession(StudioAuth.Tenant);
        var firstUser = (await studioQs.Query<StudioUser>()
            .OrderBy(u => u.CreatedAt).Take(1).ToListAsync(ct)).FirstOrDefault();
        if (firstUser is null) return;   // لا مُستَخدِمين بَعد — لا شَيء لِنَربِطه

        // مَتاجِر بِلا مالِك.
        await using var qs = store.QuerySession();
        var orphans = (await qs.Query<Tenant>()
            .Where(t => t.OwnerUserId == Guid.Empty).ToListAsync(ct)).ToList();
        if (orphans.Count == 0) return;

        await using var ws = store.LightweightSession();
        foreach (var t in orphans)
        {
            t.OwnerUserId = firstUser.Id;
            ws.Store(t);
        }
        await ws.SaveChangesAsync(ct);
    }
}
