using ACommerce.Chat.Operations;
using ACommerce.Kits.DynamicAttributes.Operations;
using ACommerce.Kits.Listings.Domain;
using ACommerce.Kits.Profiles.Operations;
using ACommerce.SharedKernel.Domain.Entities;

namespace Ashare.V3.Domain;

// ═════════════════════════════════════════════════════════════════════════
// Ashare V3 Domain — كَيانات مُطابِقَة لِجَداوِل asharedb (لا Ejar).
//
// كُلّ كَيان يُنَفِّذ <see cref="IBaseEntity"/> (Id/CreatedAt/UpdatedAt/IsDeleted)
// — مُطابِق لِنَمَط Ashare V1/V2. الكَيانات التي تَخدُم kits تُنَفِّذ واجِهات
// الكيت مُباشَرَةً (Law 6: explicit interface implementation حَيث الأَسماء تَختَلِف).
//
// نَمَط الهَويَّة (Identity hybrid):
//   - AspNetUsers.Id  (string)  — مِن ASP.NET Identity (جَدول قائِم في asharedb)
//   - Profile.Id      (Guid)    — هَويَّة طَبَقَة الـ business
//   - Profile.UserId  (string)  — يَربط Profile بِـ AspNetUsers
//   - Vendor/Host... (Guid)     — يُشيرون لِـ Profile.Id
//   - Customer/Sender (string)  — يُشيرون لِـ AspNetUsers.Id (و Profile.UserId)
// ═════════════════════════════════════════════════════════════════════════


// ─── Profile ───────────────────────────────────────────────────────────
// الجَدول مَطابِق <b>تَماماً</b> لِواجِهَة <see cref="IUserProfile"/> + هَويَّتَين
// سَطحِيَّتَين فَقَط لِخِدمَة التَطبيق (UserId, NationalId).
//
// أَيّ شَيء آخَر (BusinessName/Type/IsActive/IsVerified/VerifiedAt/Address/
// Country/PostalCode/Coordinates) ⇒ <c>AttributesJson</c>. الـ Clone يَنقُل
// قِيَم الإنتاج إلى الـ JSON أَثناء النَّسخ.
//
// <para><b>NationalId</b> يَبقى عَموداً لِأَنّه مِفتاح lookup لِـ
// Nafath flow (يُستَفسَر عَنه قَبل وُجود JWT) ⇒ JSON_VALUE عَلى كُلّ
// login مُكلِف بِلا index. UserId يَبقى لِأَنّه primary identity (يَربط
// AspNetUsers.Id بِكُلّ الجَداوِل المُتَفَرِّعَة).</para>
public class ProfileEntity : IBaseEntity, IUserProfile, IHasDynamicAttributes
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    // ─ Identity columns (لا تَنتَمي لِواجِهَة الكيت، تَبقى لِلأداء) ─
    public string? UserId { get; set; }          // ربط بِـ AspNetUsers.Id
    public string? NationalId { get; set; }      // lookup لِـ Nafath قَبل الـ JWT

    // ─ Interface columns (IUserProfile) ─
    public string? FullName { get; set; }
    public string? Phone { get; set; }
    public bool PhoneVerified { get; set; }
    public string? Email { get; set; }
    public bool EmailVerified { get; set; }
    public string? City { get; set; }
    public string? AvatarUrl { get; set; }

    // ─ Dynamic attrs snapshot ─
    /// <summary>JSON <c>{key:value}</c> لِكُلّ ما لا يَنتَمي لِلواجِهَة:
    /// BusinessName, Type, IsActive, IsVerified, VerifiedAt, Address,
    /// Country, PostalCode, Coordinates، وأَيّ زِيادَة admin. التَعريفات
    /// في <c>AttributeDefinitions</c> تَحت sentinel
    /// <c>V3ProfileAttributes.CategoryId</c>.</summary>
    public string? AttributesJson { get; set; }

    // ─── IHasDynamicAttributes — النِطاق ثابِت sentinel لِبروفايل ──
    Guid? IHasDynamicAttributes.DynamicAttributeScopeId =>
        new Guid("00000000-0000-0000-0000-000000000F01");  // = V3ProfileAttributes.CategoryId

    // ─── IUserProfile explicit impl — Id يُرَدّ كَـ UserId ──
    string  IUserProfile.Id            => UserId ?? Id.ToString();
    string  IUserProfile.FullName      => FullName ?? "";
    string  IUserProfile.Phone         => Phone ?? "";
    bool    IUserProfile.PhoneVerified => PhoneVerified;
    string? IUserProfile.Email         => Email;
    bool    IUserProfile.EmailVerified => EmailVerified;
    string  IUserProfile.City          => City ?? "";
    string? IUserProfile.AvatarUrl     => AvatarUrl;
    DateTime IUserProfile.MemberSince  => CreatedAt;
}


// ─── Products (catalog parent) + ProductCategory + ProductListing ─────
public class ProductEntity : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public string Name { get; set; } = "";
    public string Sku { get; set; } = "";
    public string Type { get; set; } = "";
    public string Status { get; set; } = "";
    public string? ShortDescription { get; set; }
    public string? LongDescription { get; set; }
    public string? Barcode { get; set; }
    public decimal? Weight { get; set; }
    public Guid? WeightUnitId { get; set; }
    public decimal? Length { get; set; }
    public decimal? Width { get; set; }
    public decimal? Height { get; set; }
    public Guid? DimensionUnitId { get; set; }
    public string? FeaturedImage { get; set; }
    public int SortOrder { get; set; }
    public bool IsFeatured { get; set; }
    public bool IsNew { get; set; }
    public DateTime? NewUntil { get; set; }
    public Guid? ParentProductId { get; set; }
}

public class ProductCategoryEntity : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public string? Description { get; set; }
    public string? Image { get; set; }
    public string? Icon { get; set; }
    public Guid? ParentCategoryId { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
    public Guid? ProductId { get; set; }
}

/// <summary>
/// Ashare's listing — implements <see cref="IListing"/> from Listings kit.
/// </summary>
public class ProductListingEntity : IBaseEntity, IListing, IHasDynamicAttributes
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public Guid VendorId { get; set; }            // → Profile.Id
    public Guid ProductId { get; set; }
    public Guid? CategoryId { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string? VendorSku { get; set; }
    public int Status { get; set; }
    public decimal Price { get; set; }
    public decimal? CompareAtPrice { get; set; }
    public decimal? Cost { get; set; }
    public Guid? CurrencyId { get; set; }
    public int QuantityAvailable { get; set; }
    public int QuantityReserved { get; set; }
    public int? LowStockThreshold { get; set; }
    public int? ProcessingTime { get; set; }
    public string? VendorNotes { get; set; }
    public DateTime? StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
    public bool IsActive { get; set; }
    public bool IsFeatured { get; set; }
    public bool IsNew { get; set; }
    public int TotalSales { get; set; }
    public int ViewCount { get; set; }
    public decimal? Rating { get; set; }
    public int ReviewCount { get; set; }
    public string? ImagesJson { get; set; }        // JSON array of URLs
    public string? FeaturedImage { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Condition { get; set; }
    public string? Currency { get; set; }
    public string? AttributesJson { get; set; }    // dynamic attrs (V2 pattern)
    public decimal CommissionPercentage { get; set; }

    // ─── Interface-aligned columns (مَضافَة لِتُغَطّي IListing) ─────
    /// <summary>"daily" | "monthly" | "yearly" | … — مَنقول مَن AttributesJson عِندَ الترحيل.</summary>
    public string? TimeUnit { get; set; }
    public int BedroomCount { get; set; }
    public int BathroomCount { get; set; }
    public int AreaSqm { get; set; }
    /// <summary>JSON array مَن slugs (مَن Discovery.Amenities). مَنقول مَن AttributesJson عِندَ الترحيل.</summary>
    public string? AmenitiesJson { get; set; }

    // ─── IHasDynamicAttributes — النِطاق = CategoryId الفِئَة ──
    Guid? IHasDynamicAttributes.DynamicAttributeScopeId => CategoryId;

    // ─── IListing explicit implementation — Ashare names ↔ kit names ──
    string IListing.Id              => Id.ToString();
    string IListing.OwnerId         => VendorId.ToString();
    string IListing.Title           => Title;
    string IListing.Description     => Description ?? "";
    decimal IListing.Price          => Price;
    string IListing.TimeUnit        => TimeUnit ?? "monthly";
    string IListing.PropertyType    => Condition ?? "";
    string IListing.City            => City ?? "";
    string IListing.District        => Address ?? "";
    double IListing.Lat             => Latitude  ?? 0;
    double IListing.Lng             => Longitude ?? 0;
    int IListing.BedroomCount       => BedroomCount;
    int IListing.BathroomCount      => BathroomCount;
    int IListing.AreaSqm            => AreaSqm;
    int IListing.Status             => IsActive ? 1 : 2;
    int IListing.ViewsCount         => ViewCount;
    bool IListing.IsVerified        => IsFeatured;
    string? IListing.ThumbnailUrl   => FeaturedImage;
    IReadOnlyList<string> IListing.Images =>
        string.IsNullOrEmpty(ImagesJson)
            ? Array.Empty<string>()
            : (System.Text.Json.JsonSerializer.Deserialize<string[]>(ImagesJson) ?? Array.Empty<string>());
    IReadOnlyList<string> IListing.Amenities =>
        string.IsNullOrEmpty(AmenitiesJson)
            ? Array.Empty<string>()
            : (System.Text.Json.JsonSerializer.Deserialize<string[]>(AmenitiesJson) ?? Array.Empty<string>());
    DateTime IListing.CreatedAt     => CreatedAt;
    DateTime? IListing.UpdatedAt    => UpdatedAt;
}


// ─── Chat + ChatParticipant + Message + MessageRead ──────────────────
public class ChatEntity : IBaseEntity, IChatConversation
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public string Title { get; set; } = "";
    public int Type { get; set; }                 // 0=direct, 1=group, …
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }

    // Navigation — populated by query, not persisted directly
    public List<ChatParticipantEntity> Participants { get; set; } = new();

    string IChatConversation.Id => Id.ToString();
    IReadOnlyList<string> IChatConversation.ParticipantPartyIds =>
        Participants.Select(p => p.UserId).Where(s => !string.IsNullOrEmpty(s)).ToList()!;
}

public class ChatParticipantEntity : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public Guid ChatId { get; set; }
    public string UserId { get; set; } = "";       // → AspNetUsers.Id
    public int Role { get; set; }                  // 0=member, 1=admin, …
    public DateTime? LastSeenMessageAt { get; set; }
    public Guid? LastSeenMessageId { get; set; }
    public bool IsMuted { get; set; }
    public bool IsPinned { get; set; }
}

public class MessageEntity : IBaseEntity, IChatMessage
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public Guid ChatId { get; set; }
    public string SenderId { get; set; } = "";    // → AspNetUsers.Id
    public string Content { get; set; } = "";
    public int Type { get; set; }                 // 0=text, 1=image, …
    public Guid? ReplyToMessageId { get; set; }
    public bool IsEdited { get; set; }
    public DateTime? EditedAt { get; set; }

    string IChatMessage.Id             => Id.ToString();
    string IChatMessage.ConversationId => ChatId.ToString();
    string IChatMessage.SenderPartyId  => SenderId;
    string IChatMessage.Body           => Content;
    DateTime IChatMessage.SentAt       => CreatedAt;
    /// <summary>
    /// Ashare يَستَخدِم جَدول <c>MessageRead</c> مُنفَصِل (per-user). <c>ReadAt</c>
    /// عَلى المُستَوى الرَئيسيّ غَير دَقيق في n-participant chats — نُرجِع null
    /// هُنا و الـ Store يَحسِب unread عَبر MessageRead لاحِقاً.
    /// </summary>
    DateTime? IChatMessage.ReadAt      => null;
}

public class MessageReadEntity : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public Guid MessageId { get; set; }
    public string UserId { get; set; } = "";       // → AspNetUsers.Id
    public DateTime ReadAt { get; set; }
}


// ─── Complaint + ComplaintReply (Ashare's support tickets) ────────────
public class ComplaintEntity : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public string UserId { get; set; } = "";       // → AspNetUsers.Id
    public string TicketNumber { get; set; } = "";
    public string Type { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Status { get; set; } = "open";   // open/in_progress/resolved/closed
    public string Priority { get; set; } = "normal";
    public string Category { get; set; } = "";
    public string? RelatedEntityType { get; set; }
    public Guid? RelatedEntityId { get; set; }
    public string? AssignedToId { get; set; }
    public string? AssignedToName { get; set; }
    public DateTime? ClosedAt { get; set; }
    public int? UserRating { get; set; }
    public string? UserFeedback { get; set; }
}

public class ComplaintReplyEntity : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public Guid ComplaintId { get; set; }
    public string SenderId { get; set; } = "";    // → AspNetUsers.Id
    public string SenderName { get; set; } = "";
    public bool IsStaff { get; set; }
    public string Message { get; set; } = "";
    public bool IsInternal { get; set; }
}


// ─── Booking + BookingStatusHistory (rental bookings) ────────────────
public class BookingEntity : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public Guid SpaceId { get; set; }              // → ProductListing.Id
    public string CustomerId { get; set; } = "";   // → AspNetUsers.Id
    public Guid HostId { get; set; }                // → Profile.Id
    public string? SpaceName { get; set; }
    public string? SpaceImage { get; set; }
    public string? SpaceLocation { get; set; }
    public DateTime CheckInDate { get; set; }
    public DateTime CheckOutDate { get; set; }
    public int RentType { get; set; }
    public decimal TotalPrice { get; set; }
    public decimal DepositPercentage { get; set; }
    public decimal DepositAmount { get; set; }
    public string Currency { get; set; } = "YER";
    public string? DepositPaymentId { get; set; }
    public DateTime? DepositPaidAt { get; set; }
    public string? FinalPaymentId { get; set; }
    public DateTime? FinalPaymentAt { get; set; }
    public int EscrowStatus { get; set; }
    public DateTime? EscrowReleasedAt { get; set; }
    public decimal? EscrowReleasedAmount { get; set; }
    public int Status { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }
    public string? CancelledBy { get; set; }
    public DateTime? RejectedAt { get; set; }
    public string? RejectionReason { get; set; }
    public string? CustomerNotes { get; set; }
    public string? HostNotes { get; set; }
    public string? InternalNotes { get; set; }
    public int GuestsCount { get; set; }
    public Guid? ReviewId { get; set; }
}

public class BookingStatusHistoryEntity : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public Guid BookingId { get; set; }
    public int OldStatus { get; set; }
    public int NewStatus { get; set; }
    public string ChangedById { get; set; } = "";
    public string? Notes { get; set; }
}


// ─── DeviceTokens + AppVersions + LegalPage ───────────────────────────
public class DeviceTokenEntity : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public string UserId { get; set; } = "";       // → AspNetUsers.Id
    public string Token { get; set; } = "";
    public string Platform { get; set; } = "fcm";
    public DateTime RegisteredAt { get; set; }
    public DateTime LastUsedAt { get; set; }
    public bool IsActive { get; set; }
    public string? AppVersion { get; set; }
    public string? DeviceModel { get; set; }
    public string? MetadataJson { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

public class AppVersionEntity : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public string ApplicationCode { get; set; } = "";
    public string ApplicationNameAr { get; set; } = "";
    public string ApplicationNameEn { get; set; } = "";
    public string VersionNumber { get; set; } = "1.0.0";
    public int BuildNumber { get; set; }
    public int Status { get; set; }
    public DateTime ReleaseDate { get; set; }
    public DateTime? DeprecationStartDate { get; set; }
    public DateTime? EndOfSupportDate { get; set; }
    public string? ReleaseNotesAr { get; set; }
    public string? ReleaseNotesEn { get; set; }
    public string? UpdateUrl { get; set; }
    public string? DownloadUrl { get; set; }
    public bool IsForceUpdate { get; set; }
    public string? MinimumSupportedVersion { get; set; }
    public bool IsActive { get; set; }
    public string? Metadata { get; set; }
}

public class LegalPageEntity : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public string Key { get; set; } = "";
    public string TitleAr { get; set; } = "";
    public string TitleEn { get; set; } = "";
    public string Url { get; set; } = "";
    public string? Icon { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
}


// ─── NEW tables (additive — لا تَمَسّ بَيانات قائِمَة) ──────────────
// هذه الجَداوِل غَير مَوجودَة في asharedb — Migration "InitialNewTables"
// يُنشِئها فَقَط، لا أَيّ ALTER عَلى الجَداوِل القائِمَة.

public class FavoriteEntity : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public string UserId { get; set; } = "";       // → AspNetUsers.Id (مُتَّسِق مَع باقي asharedb)
    public Guid ListingId { get; set; }            // → ProductListing.Id
}

public class ReportEntity : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public string ReporterId { get; set; } = "";  // → AspNetUsers.Id
    public string EntityType { get; set; } = "Listing";
    public Guid EntityId { get; set; }
    public string Reason { get; set; } = "";
    public string? Description { get; set; }
    public string Status { get; set; } = "pending";
    public DateTime? ResolvedAt { get; set; }
    public string? ResolvedById { get; set; }
    public string? ResolutionNotes { get; set; }
}

public class NotificationEntity : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public string UserId { get; set; } = "";       // → AspNetUsers.Id
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public string Kind { get; set; } = "system";   // system / message / listing / promo / support
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public string? DeepLinkUrl { get; set; }
    public string? MetadataJson { get; set; }
}


// ─── Locations (Cities + hierarchy) ─────────────────────────────────────
// مَطبوعَة عَلى Locations.Abstractions في asharedb (راجِع
// docs/projects/locations/01-Locations.Abstractions.md). جَداوِل Countries,
// Regions, Cities, Neighborhoods مَوجودَة في الإنتاج لكِنّ V3 لَم يَكُن
// يَستَنسِخها. الآن نَقرَأ مِنها لِـ /cities + /amenities + filters.

public class CountryEntity : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public string Name { get; set; } = "";
    public string? NameEn { get; set; }
    public string Code { get; set; } = "";           // ISO 3166-1 alpha-2
    public string? Code3 { get; set; }
    public int? NumericCode { get; set; }
    public string? PhoneCode { get; set; }
    public string? CurrencyCode { get; set; }
    public string? CurrencyName { get; set; }
    public string? CurrencySymbol { get; set; }
    public string? Flag { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? Timezone { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}

public class RegionEntity : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public string Name { get; set; } = "";
    public string? NameEn { get; set; }
    public string? Code { get; set; }
    /// <summary>1=Region, 2=Emirate, 3=Governorate, 4=State, 5=Province.</summary>
    public int Type { get; set; } = 1;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public Guid CountryId { get; set; }
}

public class CityEntity : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public string Name { get; set; } = "";
    public string? NameEn { get; set; }
    public string? Code { get; set; }
    public int? Population { get; set; }
    public bool IsCapital { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public Guid RegionId { get; set; }
}

public class NeighborhoodEntity : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public string Name { get; set; } = "";
    public string? NameEn { get; set; }
    public string? Code { get; set; }
    public string? PostalCode { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? Boundaries { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public Guid CityId { get; set; }
}


// ─── Category Attribute Templates (hybrid: code-canonical + DB-served) ──
/// <summary>
/// لِكُلّ فِئَة (<c>ProductCategory.Slug</c>) قالَب سِمات ديناميكِيَّة (JSON
/// لِـ <see cref="ACommerce.SharedKernel.Domain.DynamicAttributes.AttributeTemplate"/>).
///
/// <para><b>سياسَة المَزامَنَة</b>:</para>
/// <list type="bullet">
///   <item>الكود (<c>V3CategoryTemplates</c>) = المَرجِع الكانوني.</item>
///   <item>Bootstrap يَنسَخ مِن الكود إلى DB لَو الـ row ناقِص أَو لَو
///         <c>CodeVersion</c> الكود أَحدَث وَ <c>IsLockedByAdmin = false</c>.</item>
///   <item>لوحَة التَحَكُّم تُعَدِّل DB row وتَضَع <c>IsLockedByAdmin = true</c>
///         ⇒ Bootstrap لا يُلامِس هذا الـ row بَعد ذلك.</item>
///   <item>الواجِهَة تَقرَأ مِن endpoint <c>/categories/{slug}/attribute-template</c>
///         الَّذي يَقرَأ DB أَوَّلاً ثُمّ fallback لِلكود.</item>
/// </list>
/// </summary>
public class CategoryAttributeTemplateEntity : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    /// <summary>مِفتاح الفِئَة — <c>ProductCategory.Slug</c>.</summary>
    public string CategorySlug { get; set; } = "";
    /// <summary>JSON مُتَسَلسَل لِـ <c>AttributeTemplate</c>.</summary>
    public string TemplateJson { get; set; } = "";
    /// <summary>إصدار الكود الَّذي وَلَّد هذا الـ row. Bootstrap يُقارِنه قَبل overwrite.</summary>
    public int CodeVersion { get; set; }
    /// <summary>إذا <c>true</c>، Bootstrap لا يُلامِس هذا الـ row حَتّى لَو الكود تَحَدَّث.</summary>
    public bool IsLockedByAdmin { get; set; }
}


// ─── Production Attribute system (asharedb existing tables) ──────────────
// نِظام السِمات المُنَظَّم في asharedb. AttributeDefinitions يُحَدِّد ما
// هي السِمات، AttributeValues يُحَدِّد الخِيارات لِأَنواع select/multi،
// CategoryAttributeMappings يَربِط أَيّ سِمات تَخُصّ أَيّ فِئَة.
// المَصدَر: /tmp/ACommerce.Libraries/Other/ACommerce.Catalog.Attributes/Entities/.

public class AttributeDefinitionEntity : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public string Name { get; set; } = "";       // عَرَبي عادَةً (اللون، المساحة، …)
    public string Code { get; set; } = "";       // مِفتاح ثابِت (color, area_sqm, …)
    /// <summary>اسم enum مَحفوظ كَنَصّ في asharedb (HasConversion&lt;string&gt;):
    /// SingleSelect, MultiSelect, Number, Text, LongText, Boolean, Date, DateTime,
    /// File, Color.</summary>
    public string Type { get; set; } = "Text";
    public string? Description { get; set; }
    public bool IsRequired { get; set; }
    public bool IsFilterable { get; set; }
    public bool IsVisibleInList { get; set; } = true;
    public bool IsVisibleInDetail { get; set; } = true;
    public int SortOrder { get; set; }
    public string? ValidationRules { get; set; }
    public string? DefaultValue { get; set; }
}

public class AttributeValueEntity : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public Guid AttributeDefinitionId { get; set; }
    public string Value { get; set; } = "";           // المِفتاح: "red"
    public string? DisplayName { get; set; }          // العَرض: "أحمر"
    public string? Code { get; set; }                 // مُختَصَر اختياري
    public string? Description { get; set; }
    public string? ColorHex { get; set; }
    public string? ImageUrl { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public class CategoryAttributeMappingEntity : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public Guid CategoryId { get; set; }
    public Guid AttributeDefinitionId { get; set; }
    public int SortOrder { get; set; }
    public bool? IsRequiredOverride { get; set; }
    public bool IsActive { get; set; } = true;
}

public class AttributeValueRelationshipEntity : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public Guid ParentValueId { get; set; }
    public Guid ChildValueId { get; set; }
}


// ─── Listing Payments (V3-additive) ──────────────────────────────────────
/// <summary>
/// سِجلّ دَفع لِكُلّ إعلان (V3 لا يَستَخدِم باقات اشتِراك). كُلّ مُحاوَلَة
/// نَشر إعلان تَستَلزِم سِجل <c>ListingPayment</c> بِـ <c>Status="captured"</c>
/// مَوسوم لِنَفس المُستَخدِم.
///
/// <para>الـ <c>PaymentGateInterceptor</c> يَفحَص هذا الجَدول قَبل
/// السَّماح بِـ <c>listing.create</c>. الـ <c>Reference</c> هو
/// <c>PaymentReference</c> الَّذي رَدَّه <see cref="IPaymentGateway"/>.</para>
/// </summary>
public class ListingPaymentEntity : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public string UserId { get; set; } = "";          // AspNetUsers.Id
    public Guid? ListingId { get; set; }              // قَد يُملأ بَعد النَّشر
    public string Provider { get; set; } = "mock";    // gateway name
    public string Reference { get; set; } = "";       // returned by gateway
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "YER";
    /// <summary>pending / authorized / captured / failed / refunded / consumed.</summary>
    public string Status { get; set; } = "pending";
    /// <summary>عَلَم: استُهلِك هذا الدَفع لِنَشر إعلان (مَنع إعادَة الاستِخدام).</summary>
    public bool Consumed { get; set; }
    public DateTime? CapturedAt { get; set; }
}
