using ACommerce.SharedKernel.Domain.Entities;
using System.ComponentModel.DataAnnotations;

namespace ACommerce.Kits.Support.Domain;

public class SupportTicket : IBaseEntity
{
    [Key] public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    [MaxLength(200)] public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    [MaxLength(20)] public string Status { get; set; } = "open"; // open, in_progress, resolved, closed
    [MaxLength(20)] public string Priority { get; set; } = "normal";
    [MaxLength(100)] public string RelatedEntityId { get; set; } = "";
    public Guid UserId { get; set; }
    
    public List<SupportReply> Replies { get; set; } = new();
}

public class SupportReply : IBaseEntity
{
    [Key] public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public Guid TicketId { get; set; }
    [MaxLength(20)] public string FromRole { get; set; } = "user"; // user, agent, system
    public Guid AuthorId { get; set; }
    public string Message { get; set; } = "";
}
