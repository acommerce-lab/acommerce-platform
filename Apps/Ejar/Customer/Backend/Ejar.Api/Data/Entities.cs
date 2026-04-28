using System.ComponentModel.DataAnnotations;
using ACommerce.SharedKernel.Domain.Entities;

namespace Ejar.Api.Data;

// EF entities — كيانات قاعدة البيانات ترث من IBaseEntity (Guid Id, CreatedAt, UpdatedAt, IsDeleted).
// متوافقة مع BaseAsyncRepository<T> من SharedKernel.

public sealed class UserEntity : IBaseEntity
{
    [Key] public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    [MaxLength(120)] public string FullName { get; set; } = "";
    [MaxLength(20)]  public string Phone { get; set; } = "";
    public bool PhoneVerified { get; set; }
    [MaxLength(120)] public string Email { get; set; } = "";
    public bool EmailVerified { get; set; }
    [MaxLength(60)]  public string City { get; set; } = "";
    public DateTime MemberSince { get; set; }
}

public sealed class ListingEntity : IBaseEntity
{
    [Key] public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    [MaxLength(200)] public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public decimal Price { get; set; }
    [MaxLength(20)]  public string TimeUnit { get; set; } = "";
    [MaxLength(40)]  public string PropertyType { get; set; } = "";
    [MaxLength(60)]  public string City { get; set; } = "";
    [MaxLength(120)] public string District { get; set; } = "";
    public double Lat { get; set; }
    public double Lng { get; set; }
    [MaxLength(500)] public string AmenitiesCsv { get; set; } = "";
    public Guid OwnerId { get; set; }
    public int BedroomCount { get; set; }
    public int BathroomCount { get; set; }
    public int AreaSqm { get; set; }
    public bool IsVerified { get; set; }
    public int ViewsCount { get; set; }
    public int Status { get; set; } = 1;
    [MaxLength(2000)] public string ImagesCsv { get; set; } = "";
}

public sealed class ConversationEntity : IBaseEntity
{
    [Key] public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    [MaxLength(120)] public string PartnerName { get; set; } = "";
    public Guid PartnerId { get; set; }
    public Guid ListingId { get; set; }
    [MaxLength(200)] public string Subject { get; set; } = "";
    public DateTime LastAt { get; set; }
    public int UnreadCount { get; set; }
    public List<MessageEntity> Messages { get; set; } = new();
}

public sealed class MessageEntity : IBaseEntity
{
    [Key] public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public Guid ConversationId { get; set; }
    [MaxLength(64)]  public string From { get; set; } = "";
    public string Text { get; set; } = "";
    public DateTime SentAt { get; set; }
}

public sealed class NotificationEntity : IBaseEntity
{
    [Key] public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    [MaxLength(200)] public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public bool IsRead { get; set; }
    [MaxLength(64)] public string? RelatedId { get; set; }
    [MaxLength(40)] public string Type { get; set; } = "system";
}

public sealed class FavoriteEntity : IBaseEntity
{
    [Key] public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public Guid UserId { get; set; }
    public Guid ListingId { get; set; }
}

public sealed class PlanEntity : IBaseEntity
{
    [Key] public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

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

// ─── Complaint entities (لم تكن في DbContext سابقاً) ────────────────────
public sealed class ComplaintEntity : IBaseEntity
{
    [Key] public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    [MaxLength(200)] public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    [MaxLength(20)] public string Status { get; set; } = "open";
    [MaxLength(20)] public string Priority { get; set; } = "عادي";
    [MaxLength(100)] public string RelatedEntity { get; set; } = "";
    public Guid UserId { get; set; }
    public List<ComplaintReplyEntity> Replies { get; set; } = new();
}

public sealed class ComplaintReplyEntity : IBaseEntity
{
    [Key] public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public Guid ComplaintId { get; set; }
    [MaxLength(20)] public string From { get; set; } = "";
    public string Message { get; set; } = "";
}

// ─── Subscription + Invoice entities ─────────────────────────────────────
public sealed class SubscriptionEntity : IBaseEntity
{
    [Key] public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public Guid UserId { get; set; }
    public Guid PlanId { get; set; }
    [MaxLength(60)] public string PlanName { get; set; } = "";
    [MaxLength(20)] public string Status { get; set; } = "active";
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int ListingsLimit { get; set; }
    public int FeaturedLimit { get; set; }
    public int ImagesPerListing { get; set; }
}

public sealed class InvoiceEntity : IBaseEntity
{
    [Key] public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public Guid UserId { get; set; }
    public Guid PlanId { get; set; }
    public decimal Amount { get; set; }
    public DateTime Date { get; set; }
    [MaxLength(20)] public string Status { get; set; } = "paid";
}
