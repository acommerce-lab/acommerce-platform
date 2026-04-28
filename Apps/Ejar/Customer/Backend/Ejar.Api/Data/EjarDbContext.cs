using Microsoft.EntityFrameworkCore;
using ACommerce.Kits.Support.Domain;
using ACommerce.Kits.Discovery.Domain;
using ACommerce.Favorites.Operations.Entities;

namespace Ejar.Api.Data;

/// <summary>
/// قاعدة بيانات إيجار — كل الكيانات ترث IBaseEntity (Guid Id).
/// </summary>
public sealed class EjarDbContext : DbContext
{
    public EjarDbContext(DbContextOptions<EjarDbContext> options) : base(options) { }

    public DbSet<UserEntity>          Users          => Set<UserEntity>();
    public DbSet<ListingEntity>       Listings       => Set<ListingEntity>();
    public DbSet<ConversationEntity>  Conversations  => Set<ConversationEntity>();
    public DbSet<MessageEntity>       Messages       => Set<MessageEntity>();
    public DbSet<NotificationEntity>  Notifications  => Set<NotificationEntity>();
    public DbSet<Favorite>            Favorites      => Set<Favorite>();
    public DbSet<PlanEntity>          Plans          => Set<PlanEntity>();
    public DbSet<SupportTicket>       Complaints     => Set<SupportTicket>();
    public DbSet<SupportReply>        ComplaintReplies => Set<SupportReply>();
    public DbSet<SubscriptionEntity>  Subscriptions  => Set<SubscriptionEntity>();
    public DbSet<InvoiceEntity>       Invoices       => Set<InvoiceEntity>();
    
    // Discovery Kit
    public DbSet<DiscoveryCategory>   DiscoveryCategories => Set<DiscoveryCategory>();
    public DbSet<DiscoveryRegion>     DiscoveryRegions    => Set<DiscoveryRegion>();
    public DbSet<DiscoveryAmenity>    DiscoveryAmenities   => Set<DiscoveryAmenity>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<ConversationEntity>()
            .HasMany(c => c.Messages)
            .WithOne()
            .HasForeignKey(m => m.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<SupportTicket>()
            .HasMany(c => c.Replies)
            .WithOne()
            .HasForeignKey(r => r.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<ListingEntity>().HasIndex(l => l.City);
        b.Entity<ListingEntity>().HasIndex(l => l.PropertyType);
        b.Entity<ListingEntity>().HasIndex(l => l.OwnerId);
        b.Entity<UserEntity>().HasIndex(u => u.Phone).IsUnique();
        b.Entity<Favorite>().HasIndex(f => new { f.UserId, f.EntityType, f.EntityId }).IsUnique();
        b.Entity<MessageEntity>().HasIndex(m => m.ConversationId);

        // Global query filter: لا تُرجع الكيانات المحذوفة افتراضياً
        b.Entity<UserEntity>().HasQueryFilter(e => !e.IsDeleted);
        b.Entity<ListingEntity>().HasQueryFilter(e => !e.IsDeleted);
        b.Entity<ConversationEntity>().HasQueryFilter(e => !e.IsDeleted);
        b.Entity<MessageEntity>().HasQueryFilter(e => !e.IsDeleted);
        b.Entity<NotificationEntity>().HasQueryFilter(e => !e.IsDeleted);
        b.Entity<Favorite>().HasQueryFilter(e => !e.IsDeleted);
        b.Entity<SupportTicket>().HasQueryFilter(e => !e.IsDeleted);
        
        b.Entity<DiscoveryCategory>().HasIndex(c => c.Slug).IsUnique();
        b.Entity<DiscoveryRegion>().HasIndex(r => r.Name);
        b.Entity<DiscoveryAmenity>().HasIndex(a => a.Slug).IsUnique();
    }
}
