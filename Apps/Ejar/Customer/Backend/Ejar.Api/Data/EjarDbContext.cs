using Microsoft.EntityFrameworkCore;

namespace Ejar.Api.Data;

/// <summary>
/// قاعدة بيانات إيجار. مزوّد قاعدة البيانات (sqlite/mssql) يُختار في
/// <see cref="DatabaseRegistration.AddEjarDatabase"/> بناءً على
/// <c>Database:Provider</c> في appsettings.
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

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<ConversationEntity>()
            .HasMany(c => c.Messages)
            .WithOne()
            .HasForeignKey(m => m.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<ListingEntity>().HasIndex(l => l.City);
        b.Entity<ListingEntity>().HasIndex(l => l.PropertyType);
        b.Entity<UserEntity>().HasIndex(u => u.Phone).IsUnique();
        b.Entity<FavoriteEntity>().HasIndex(f => new { f.UserId, f.ListingId }).IsUnique();
        b.Entity<MessageEntity>().HasIndex(m => m.ConversationId);
    }
}
