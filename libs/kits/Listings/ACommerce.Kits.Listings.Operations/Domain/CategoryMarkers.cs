namespace ACommerce.Kits.Listings.Domain;

// ════════════════════════════════════════════════════════════════════════
// Placeholder interfaces — typed-kits direction.
//
// راجِع <see href="docs/LISTINGS-TYPED-KITS.md"/> لِلتَفاصيل. هذه الواجِهات
// مُجَرَّدَة الآن (marker فَقَط) ليَعبُر التَطبيق فيها كَ promised contract
// مُستَقبَلي. عِندَ بِناء كيت فِعلي لِكُلّ عائِلَة (Realty/Vehicle/Event/
// Apparel)، الـ interface يَنتَقِل لِكيت مُستَقِلّ + يَحصُل عَلى أَعضائه.
//
// لا تَستَخدِم هذه الواجِهَات الآن لِلتَّمييز في الكود — قُرَّاء الكود
// قَد يَلجَؤون لِـ <see cref="IListing.PropertyType"/> + شَجَرَة Taxonomy
// لِلتَّصنيف.
// ════════════════════════════════════════════════════════════════════════

/// <summary>
/// عَلامَة فارِغَة لِإعلان عَقاري (شَقَّة/فيلا/مَكتَب/…). عِندَ بِناء كيت
/// <c>Listings.Realty</c>، تَتَوَسَّع لِتَحوي <c>BedroomCount</c>،
/// <c>BathroomCount</c>، <c>AreaSqm</c>، <c>Floor</c>، إلخ.
/// </summary>
public interface IRealtyListing : IListing { }

/// <summary>
/// عَلامَة فارِغَة لِإعلان مَركَبَة (سَيّارَة/باص/درّاجَة/دَينا/…). عِندَ
/// بِناء كيت <c>Listings.Vehicle</c>، تَتَوَسَّع لِتَحوي <c>Make</c>،
/// <c>Model</c>، <c>Year</c>، <c>Mileage</c>، <c>FuelType</c>،
/// <c>Transmission</c>، <c>HasDriver</c>.
/// </summary>
public interface IVehicleListing : IListing { }

/// <summary>
/// عَلامَة فارِغَة لِإعلان مُناسَبَة (صالَة/كوشَة/مُخَيَّم أَفراح/…). عِندَ
/// بِناء كيت <c>Listings.Event</c>، تَتَوَسَّع لِتَحوي <c>Capacity</c>،
/// <c>IndoorOutdoor</c>، <c>HasStage</c>، <c>CateringIncluded</c>،
/// <c>AvailableDates</c>.
/// </summary>
public interface IEventListing : IListing { }

/// <summary>
/// عَلامَة فارِغَة لِإعلان ملابس (عَرسان/مَواليد/يَومي/…). عِندَ بِناء
/// كيت <c>Listings.Apparel</c>، تَتَوَسَّع لِتَحوي <c>Size</c>،
/// <c>Color</c>، <c>Material</c>، <c>GenderTarget</c>، <c>BrandName</c>.
/// </summary>
public interface IApparelListing : IListing { }
