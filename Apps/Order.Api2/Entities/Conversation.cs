using ACommerce.SharedKernel.Abstractions.Entities;

namespace Order.Api2.Entities;

public class Conversation : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public Guid CustomerId { get; set; }
    public Guid VendorId { get; set; }
    public Guid? OrderId { get; set; }   // optional: chat about a specific order

    public string? LastMessageSnippet { get; set; }
    public DateTime? LastMessageAt { get; set; }
    public int UnreadCustomerCount { get; set; }
    public int UnreadVendorCount { get; set; }
}

public class Message : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public Guid ConversationId { get; set; }
    public Guid SenderId { get; set; }
    public string Content { get; set; } = default!;
}
