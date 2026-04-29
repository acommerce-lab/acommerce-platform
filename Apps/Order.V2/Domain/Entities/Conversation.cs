using ACommerce.Chat.Operations;
using ACommerce.SharedKernel.Domain.Entities;

namespace Order.V2.Domain;

public class Conversation : IBaseEntity, IChatConversation
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public Guid CustomerId { get; set; }
    public Guid VendorId { get; set; }
    public Guid? OrderId { get; set; }

    public string? LastMessageSnippet { get; set; }
    public DateTime? LastMessageAt { get; set; }
    public int UnreadCustomerCount { get; set; }
    public int UnreadVendorCount { get; set; }

    // IChatConversation (interface view — domain storage unchanged; Law 6).
    string IChatConversation.Id => Id.ToString();
    IReadOnlyList<string> IChatConversation.ParticipantPartyIds
        => new[] { $"User:{CustomerId}", $"Vendor:{VendorId}" };
}

public class Message : IBaseEntity, IChatMessage
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public Guid ConversationId { get; set; }
    public Guid SenderId { get; set; }
    public string Content { get; set; } = default!;

    // IChatMessage (interface view; Law 6 amended).
    string IChatMessage.Id               => Id.ToString();
    string IChatMessage.ConversationId   => ConversationId.ToString();
    string IChatMessage.SenderPartyId    => $"User:{SenderId}";
    string IChatMessage.Body             => Content;
    DateTime IChatMessage.SentAt         => CreatedAt;
    DateTime? IChatMessage.ReadAt        => null;
}
