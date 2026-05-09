using ACommerce.SharedKernel.Domain.Entities;

namespace Ashare.V2.Domain;

public class Profile : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public string? FullName { get; set; }
    public string? NationalId { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public string? AvatarUrl { get; set; }
    public string? City { get; set; }
    public bool NafathVerified { get; set; }
    public bool IsActive { get; set; }
    public string Role { get; set; } = "customer";
}

public class TwoFactorChallengeRecord : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public string TransactionId { get; set; } = default!;
    public string NationalId { get; set; } = default!;
    public string Status { get; set; } = "pending";
    public DateTime ExpiresAt { get; set; }
}

public class DeviceTokenEntity : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public Guid UserId { get; set; }
    public string Token { get; set; } = default!;
    public string Platform { get; set; } = "fcm";
}

public class ProductCategory : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public string Name { get; set; } = default!;
    public string? Icon { get; set; }
    public Guid? ParentId { get; set; }
    public int SortOrder { get; set; }
}

public class AttributeDefinition : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public string Key { get; set; } = default!;
    public string Label { get; set; } = default!;
    public string InputType { get; set; } = "text";
    public bool IsRequired { get; set; }
    public string? OptionsJson { get; set; }
}

public class CategoryAttributeMapping : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public Guid CategoryId { get; set; }
    public Guid AttributeId { get; set; }
    public int SortOrder { get; set; }
}

public class Product : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public Guid OwnerId { get; set; }
    public Guid CategoryId { get; set; }
    public string Title { get; set; } = default!;
    public string? Description { get; set; }
    public string? DynamicAttributesJson { get; set; }
}

public class ProductListing : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public Guid ProductId { get; set; }
    public Guid OwnerId { get; set; }
    public decimal Price { get; set; }
    public string TimeUnit { get; set; } = "day";
    public string Currency { get; set; } = "SAR";
    public string City { get; set; } = default!;
    public string? District { get; set; }
    public double? Lat { get; set; }
    public double? Lng { get; set; }
    public int Status { get; set; }
    public bool IsFeatured { get; set; }
    public int ViewCount { get; set; }
    public string? ImagesCsv { get; set; }
    public Guid? SubscriptionId { get; set; }
}

public class Order : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public Guid CustomerId { get; set; }
    public Guid ListingId { get; set; }
    public string Status { get; set; } = "pending";
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "SAR";
}

public class Booking : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public Guid CustomerId { get; set; }
    public Guid ListingId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Status { get; set; } = "pending";
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "SAR";
    public Guid? PaymentId { get; set; }
}

public class Payment : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public Guid BookingId { get; set; }
    public Guid PayerId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "SAR";
    public string Gateway { get; set; } = "noon";
    public string Status { get; set; } = "pending";
    public string? GatewayOrderId { get; set; }
    public string? GatewayReference { get; set; }
}

public class Subscription : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public Guid OwnerId { get; set; }
    public string PlanKey { get; set; } = default!;
    public int ListingsLimit { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public string Status { get; set; } = "active";
}

public class Chat : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public Guid ParticipantA { get; set; }
    public Guid ParticipantB { get; set; }
    public Guid? ListingId { get; set; }
    public DateTime? LastMessageAt { get; set; }
}

public class ChatMessage : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public Guid ChatId { get; set; }
    public Guid SenderId { get; set; }
    public string Body { get; set; } = default!;
    public string? AttachmentUrl { get; set; }
    public bool IsRead { get; set; }
}

public class Notification : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public Guid UserId { get; set; }
    public string Title { get; set; } = default!;
    public string Body { get; set; } = default!;
    public string Type { get; set; } = "general";
    public bool IsRead { get; set; }
    public string? Payload { get; set; }
}

public class Complaint : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public Guid ReporterId { get; set; }
    public Guid? ListingId { get; set; }
    public Guid? BookingId { get; set; }
    public string Category { get; set; } = default!;
    public string Body { get; set; } = default!;
    public string Status { get; set; } = "open";
}

public class ComplaintReply : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public Guid ComplaintId { get; set; }
    public Guid AuthorId { get; set; }
    public string Body { get; set; } = default!;
    public bool IsStaff { get; set; }
}

public class LegalPage : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public string Slug { get; set; } = default!;
    public string TitleAr { get; set; } = default!;
    public string TitleEn { get; set; } = default!;
    public string BodyAr { get; set; } = default!;
    public string BodyEn { get; set; } = default!;
    public int SortOrder { get; set; }
}

public class AttributionSession : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public Guid? UserId { get; set; }
    public string SessionToken { get; set; } = default!;
    public string? UtmSource { get; set; }
    public string? UtmMedium { get; set; }
    public string? UtmCampaign { get; set; }
    public string? Referrer { get; set; }
    public DateTime? ConvertedAt { get; set; }
}
