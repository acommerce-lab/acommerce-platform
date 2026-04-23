using ACommerce.SharedKernel.Abstractions.Entities;

namespace Order.V2.Api.Entities;

public class Conversation : IBaseEntity
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
