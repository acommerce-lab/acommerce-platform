using ACommerce.SharedKernel.Abstractions.Entities;

namespace Ashare.Api2.Entities;

/// <summary>
/// محادثة بين طرفين (عادة عميل ومالك عرض).
/// </summary>
public class Conversation : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public Guid? ListingId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid OwnerId { get; set; }
    public string? LastMessageSnippet { get; set; }
    public DateTime? LastMessageAt { get; set; }
    public int UnreadCustomerCount { get; set; }
    public int UnreadOwnerCount { get; set; }
}

/// <summary>
/// رسالة دردشة في محادثة.
/// </summary>
public class Message : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public Guid ConversationId { get; set; }
    public Guid SenderId { get; set; }
    public string Content { get; set; } = default!;
    public string MessageType { get; set; } = "text"; // text, image, file, system
    public string? AttachmentUrl { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
}
