using ACommerce.Chat.Operations;
using ACommerce.Kits.Listings.Domain;
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


// ─── Profile (٢٠ عَمود) ────────────────────────────────────────────────
public class ProfileEntity : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public string? UserId { get; set; }          // ربط بِـ AspNetUsers.Id
    public string? NationalId { get; set; }
    public int Type { get; set; }                 // 0=customer, 1=vendor, …
    public string? FullName { get; set; }
    public string? BusinessName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public string? Avatar { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public string? PostalCode { get; set; }
    public string? Coordinates { get; set; }
    public bool IsActive { get; set; }
    public bool IsVerified { get; set; }
    public DateTime? VerifiedAt { get; set; }
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
public class ProductListingEntity : IBaseEntity, IListing
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

    // ─── IListing explicit implementation — Ashare names ↔ kit names ──
    string IListing.Id              => Id.ToString();
    string IListing.OwnerId         => VendorId.ToString();
    string IListing.Title           => Title;
    string IListing.Description     => Description ?? "";
    decimal IListing.Price          => Price;
    string IListing.TimeUnit        => "monthly";   // Ashare default; TODO: derive from AttributesJson
    string IListing.PropertyType    => Condition ?? "";
    string IListing.City            => City ?? "";
    string IListing.District        => Address ?? "";
    double IListing.Lat             => Latitude  ?? 0;
    double IListing.Lng             => Longitude ?? 0;
    int IListing.BedroomCount       => 0;            // TODO: read from AttributesJson
    int IListing.BathroomCount      => 0;
    int IListing.AreaSqm            => 0;
    int IListing.Status             => IsActive ? 1 : 2;
    int IListing.ViewsCount         => ViewCount;
    bool IListing.IsVerified        => IsFeatured;
    string? IListing.ThumbnailUrl   => FeaturedImage;
    IReadOnlyList<string> IListing.Images =>
        string.IsNullOrEmpty(ImagesJson)
            ? Array.Empty<string>()
            : (System.Text.Json.JsonSerializer.Deserialize<string[]>(ImagesJson) ?? Array.Empty<string>());
    IReadOnlyList<string> IListing.Amenities => Array.Empty<string>();  // TODO: AttributesJson
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
