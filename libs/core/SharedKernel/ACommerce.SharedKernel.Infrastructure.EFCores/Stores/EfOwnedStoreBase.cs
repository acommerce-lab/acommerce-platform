using ACommerce.SharedKernel.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ACommerce.SharedKernel.Infrastructure.EFCore.Stores;

/// <summary>
/// قاعِدَة <c>Store</c> فَوق EF لِكَيان يَملِكه مُستَخدِم. تَختَزِل ٣ نَماذِج
/// مُكَرَّرَة في كُلّ store يَلتَزِم بِـ F6 (NoSaveAsync، الـ engine يَحفَظ):
///
/// <list type="number">
///   <item><see cref="FindByGuidStringAsync"/> — يَفُكّ string-id إلى Guid +
///         يَجلِب <c>FirstOrDefault</c>. كُلّ stores الكِيت تَبدَأ بِه.</item>
///   <item><see cref="SoftDeleteNoSaveAsync"/> — Soft delete بِـ
///         <c>IsDeleted = true</c> + <c>UpdatedAt</c>، بِلا حِفظ. يَرُدّ
///         bool: <c>true</c> لَو الكَيان وُجِد، <c>false</c> غَير ذلك.</item>
///   <item><see cref="ApplyPatchNoSaveAsync"/> — مَرَّن: يَجلِب الكَيان، يَستَدعي
///         delegate تَمرير لِيُعَدِّل، يَضَع <c>UpdatedAt</c>، بِلا حِفظ.</item>
/// </list>
///
/// <para>الاستِخدام opt-in — الـ store يَختار التَّوريث. لا ضَغط مُعَماري؛
/// الـ stores القائِمَة تَعمَل بِلا تَغيير حَتّى تُنقَل تَدريجيّاً:</para>
/// <code>
/// public sealed class EjarFavoritesStore : EfOwnedStoreBase&lt;Favorite&gt;, IFavoritesStore
/// {
///     public EjarFavoritesStore(EjarDbContext db) : base(db) { }
///     // الـ store يَحصُل عَلى Find/SoftDelete/ApplyPatch مَجّاناً.
/// }
/// </code>
/// </summary>
public abstract class EfOwnedStoreBase<TEntity>
    where TEntity : class, IBaseEntity
{
    protected DbContext Db { get; }
    protected DbSet<TEntity> Set { get; }

    protected EfOwnedStoreBase(DbContext db)
    {
        Db  = db;
        Set = db.Set<TEntity>();
    }

    /// <summary>يَفُكّ string-id إلى Guid + يَجلِب الكَيان (أَو null).
    /// <c>includeDeleted=false</c> يَتَجاوَز الـ soft-deleted (يَستَخدِم
    /// query filter لَو مُسَجَّل، وَ إلّا يَفلِتِر يَدَوياً).</summary>
    protected async Task<TEntity?> FindByGuidStringAsync(
        string id, CancellationToken ct, bool includeDeleted = false)
    {
        if (!Guid.TryParse(id, out var guid)) return null;
        var q = includeDeleted ? Set.IgnoreQueryFilters() : Set.AsQueryable();
        return await q.FirstOrDefaultAsync(e => e.Id == guid, ct);
    }

    /// <summary>Soft delete (يُسَمّى أَيضاً "tombstone"): يَضَع
    /// <c>IsDeleted = true</c> + <c>UpdatedAt = utcNow</c>، بِلا
    /// <c>SaveChangesAsync</c>. الـ engine يَحفَظ في <c>SaveAtEnd</c>.</summary>
    protected async Task<bool> SoftDeleteNoSaveAsync(string id, CancellationToken ct)
    {
        var entity = await FindByGuidStringAsync(id, ct);
        if (entity is null) return false;
        entity.IsDeleted = true;
        entity.UpdatedAt = DateTime.UtcNow;
        return true;
    }

    /// <summary>يَجلِب الكَيان، يَستَدعي <paramref name="apply"/> لِتَعديل
    /// الحُقول، يَضَع <c>UpdatedAt</c>، بِلا حِفظ. الـ <typeparamref name="TPatch"/>
    /// هو POCO/record يَحوي الحُقول الجَديدَة (الـ store يَختار شَكله).</summary>
    protected async Task<bool> ApplyPatchNoSaveAsync<TPatch>(
        string id, TPatch patch, Action<TEntity, TPatch> apply, CancellationToken ct)
    {
        var entity = await FindByGuidStringAsync(id, ct);
        if (entity is null) return false;
        apply(entity, patch);
        entity.UpdatedAt = DateTime.UtcNow;
        return true;
    }
}
