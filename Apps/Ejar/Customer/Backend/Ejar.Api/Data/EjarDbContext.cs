using Microsoft.EntityFrameworkCore;

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
    public DbSet<FavoriteEntity>      Favorites      => Set<FavoriteEntity>();
    public DbSet<PlanEntity>          Plans          => Set<PlanEntity>();
    public DbSet<ComplaintEntity>     Complaints     => Set<ComplaintEntity>();
    public DbSet<ComplaintReplyEntity> ComplaintReplies => Set<ComplaintReplyEntity>();
    public DbSet<SubscriptionEntity>  Subscriptions  => Set<SubscriptionEntity>();
    public DbSet<InvoiceEntity>       Invoices       => Set<InvoiceEntity>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<ConversationEntity>()
            .HasMany(c => c.Messages)
            .WithOne()
            .HasForeignKey(m => m.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<ComplaintEntity>()
            .HasMany(c => c.Replies)
            .WithOne()
            .HasForeignKey(r => r.ComplaintId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<ListingEntity>().HasIndex(l => l.City);
        b.Entity<ListingEntity>().HasIndex(l => l.PropertyType);
        b.Entity<ListingEntity>().HasIndex(l => l.OwnerId);
        b.Entity<UserEntity>().HasIndex(u => u.Phone).IsUnique();
        b.Entity<FavoriteEntity>().HasIndex(f => new { f.UserId, f.ListingId }).IsUnique();
        b.Entity<MessageEntity>().HasIndex(m => m.ConversationId);

        // Global query filter: لا تُرجع الكيانات المحذوفة افتراضياً
        b.Entity<UserEntity>().HasQueryFilter(e => !e.IsDeleted);
        b.Entity<ListingEntity>().HasQueryFilter(e => !e.IsDeleted);
        b.Entity<ConversationEntity>().HasQueryFilter(e => !e.IsDeleted);
        b.Entity<MessageEntity>().HasQueryFilter(e => !e.IsDeleted);
        b.Entity<NotificationEntity>().HasQueryFilter(e => !e.IsDeleted);
        b.Entity<FavoriteEntity>().HasQueryFilter(e => !e.IsDeleted);
        b.Entity<ComplaintEntity>().HasQueryFilter(e => !e.IsDeleted);
    }
}
