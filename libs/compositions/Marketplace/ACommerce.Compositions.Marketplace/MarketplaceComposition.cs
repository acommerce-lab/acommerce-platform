using ACommerce.Compositions.Core;
using ACommerce.Favorites.Operations.Entities;
using ACommerce.Kits.Discovery.Domain;
using ACommerce.Kits.Listings.Backend;
using ACommerce.Kits.Profiles.Backend;
using ACommerce.OperationEngine.Interceptors;

namespace ACommerce.Compositions.Marketplace;

/// <summary>
/// تركيب Marketplace = Listings + Discovery + Favorites + Profiles. تطبيقات
/// الـ marketplace (إيجار، عشير، …) تستهلك هذا التركيب الواحد بدل تسجيل
/// كلّ كيت يدويّاً مع التحقّق من ترتيب الاعتماديّات.
///
/// <para>المكتبة لا تحوي bundles معترضات حاليّاً. Bundles مستقبليّة
/// مرشَّحة:</para>
/// <list type="bullet">
///   <item><c>ListingCreatedNotificationBundle</c> — على
///         <c>listing.create</c>: ابحث عن المستخدمين الذين سجّلوا اهتماماً
///         بالفئة/المنطقة/السعر، ثمّ أرسل <c>notification.create</c>
///         child op لكلّ منهم.</item>
///   <item><c>ListingTrendingBundle</c> — interceptor على <c>listing.view</c>
///         يحدّث عداد الـ trending في cache منفصل (لا يلمس DB rows).</item>
///   <item><c>FavoritePriceDropBundle</c> — على <c>listing.edit</c> لو
///         <c>price</c> انخفض: notification.create لكلّ من في المفضّلة.</item>
/// </list>
/// كلّها تُضاف لاحقاً كـ <see cref="IInterceptorBundle"/> في <see cref="Bundles"/>.
/// </summary>
public sealed class MarketplaceComposition : ICompositionDescriptor
{
    public string Name => "Marketplace (Listings + Discovery + Favorites + Profiles)";

    public IEnumerable<Type> RequiredKits => new[]
    {
        typeof(IListingStore),
        typeof(IProfileStore),
        // Discovery + Favorites: لا توجد interfaces عامّة تُفحَص هنا (الكيت
        // يسجّل DataInterceptors فقط). الفحص يقتصر على الـ kits الكتابيّة.
    };

    public IEnumerable<IInterceptorBundle> Bundles => Array.Empty<IInterceptorBundle>();
}
