using System.ComponentModel.DataAnnotations;

namespace Ejar.Api.Data;

// EF entities — كيانات قاعدة البيانات. منفصلة عن EjarSeed records لإبقاء
// تبعيّات قاعدة البيانات (Mutability + ICollection + value converters)
// خارج Domain. الـ seeder يحوّل بين الاثنين عند بدء التشغيل.

public sealed class UserEntity
{
    [Key, MaxLength(32)] public string Id { get; set; } = "";
    [MaxLength(120)] public string FullName { get; set; } = "";
    [MaxLength(20)]  public string Phone { get; set; } = "";
    public bool PhoneVerified { get; set; }
    [MaxLength(120)] public string Email { get; set; } = "";
    public bool EmailVerified { get; set; }
    [MaxLength(60)]  public string City { get; set; } = "";
    public DateTime MemberSince { get; set; }
}

public sealed class ListingEntity
{
    [Key, MaxLength(32)] public string Id { get; set; } = "";
    [MaxLength(200)] public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public decimal Price { get; set; }
    [MaxLength(20)]  public string TimeUnit { get; set; } = "";          // monthly|yearly|daily|hourly
    [MaxLength(40)]  public string PropertyType { get; set; } = "";      // apartment|villa|...
    [MaxLength(60)]  public string City { get; set; } = "";
    [MaxLength(120)] public string District { get; set; } = "";
    public double Lat { get; set; }
    public double Lng { get; set; }
    /// <summary>كاوحدة قائمة مفصولة بفواصل (csv) — مزايا مثل ac,wifi,kitchen.</summary>
    [MaxLength(500)] public string AmenitiesCsv { get; set; } = "";
    [MaxLength(32)]  public string OwnerId { get; set; } = "";
    public int BedroomCount { get; set; }
    public int BathroomCount { get; set; }
    public int AreaSqm { get; set; }
    public bool IsVerified { get; set; }
    public int ViewsCount { get; set; }
    public int Status { get; set; } = 1;
    [MaxLength(2000)] public string ImagesCsv { get; set; } = "";        // قائمة URLs مفصولة بفواصل
}

public sealed class ConversationEntity
{
    [Key, MaxLength(32)] public string Id { get; set; } = "";
    [MaxLength(120)] public string PartnerName { get; set; } = "";
    [MaxLength(32)]  public string PartnerId { get; set; } = "";
    [MaxLength(32)]  public string ListingId { get; set; } = "";
    [MaxLength(200)] public string Subject { get; set; } = "";
    public DateTime LastAt { get; set; }
    public int UnreadCount { get; set; }
    public List<MessageEntity> Messages { get; set; } = new();
}

public sealed class MessageEntity
{
    [Key, MaxLength(32)] public string Id { get; set; } = "";
    [MaxLength(32)]  public string ConversationId { get; set; } = "";
    [MaxLength(32)]  public string From { get; set; } = "";              // "me" | "other" | userId
    public string Text { get; set; } = "";
    public DateTime SentAt { get; set; }
}

public sealed class NotificationEntity
{
    [Key, MaxLength(32)] public string Id { get; set; } = "";
    [MaxLength(200)] public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public bool IsRead { get; set; }
    [MaxLength(64)] public string? RelatedId { get; set; }
    [MaxLength(40)] public string Type { get; set; } = "system";
}

public sealed class FavoriteEntity
{
    [Key, MaxLength(64)] public string Id { get; set; } = "";            // userId|listingId
    [MaxLength(32)] public string UserId { get; set; } = "";
    [MaxLength(32)] public string ListingId { get; set; } = "";
}

public sealed class PlanEntity
{
    [Key, MaxLength(32)] public string Id { get; set; } = "";
    [MaxLength(60)]  public string Label { get; set; } = "";
    public decimal Price { get; set; }
    [MaxLength(20)]  public string CycleLabel { get; set; } = "";
    public int MaxActiveListings { get; set; }
    public int MaxFeaturedListings { get; set; }
    public int MaxImagesPerListing { get; set; }
    public bool IsRecommended { get; set; }
    public string Description { get; set; } = "";
    [MaxLength(2000)] public string FeaturesCsv { get; set; } = "";
}
