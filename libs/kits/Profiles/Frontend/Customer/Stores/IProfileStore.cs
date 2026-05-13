using ACommerce.Kits.Profiles.Operations;

namespace ACommerce.Kits.Profiles.Frontend.Customer.Stores;

/// <summary>store reactive لملفّ المستخدِم الحاليّ.</summary>
public interface IProfileStore
{
    IUserProfile? Current { get; }
    bool IsLoading { get; }
    event Action? Changed;

    Task LoadAsync(CancellationToken ct = default);
    /// <summary>تَحديث البروفايل. <paramref name="attributes"/> اختياري —
    /// لَو مُمَرَّر يُرسَل كَ <c>Attributes</c> في الـ PUT body ⇒ يَكتُب
    /// <c>Profile.AttributesJson</c>. <c>null</c> = "لا تَلمَس السِمات".</summary>
    Task UpdateAsync(IUserProfile next,
                     IReadOnlyDictionary<string, object?>? attributes = null,
                     CancellationToken ct = default);
}
