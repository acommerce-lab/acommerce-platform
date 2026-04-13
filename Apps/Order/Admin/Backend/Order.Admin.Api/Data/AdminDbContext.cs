using Order.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Order.Admin.Api.Data;

/// <summary>
/// DbContext لوحة إدارة اوردر - يشمل نفس مجموعات الكيانات من Customer API
/// حتى يمكن للوحة الإدارة قراءة وتعديل نفس البيانات.
/// </summary>
public class AdminDbContext : DbContext
{
    public AdminDbContext(DbContextOptions<AdminDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Vendor> Vendors => Set<Vendor>();
    public DbSet<Offer> Offers => Set<Offer>();
    public DbSet<OrderRecord> Orders => Set<OrderRecord>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Favorite> Favorites => Set<Favorite>();
    public DbSet<TwoFactorChallengeRecord> TwoFactorChallengeRecords => Set<TwoFactorChallengeRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Soft delete query filter: استبعاد المحذوفات بشكل افتراضي
        modelBuilder.Entity<User>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Category>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Vendor>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Offer>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<OrderRecord>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<OrderItem>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Conversation>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Message>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Notification>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Favorite>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<TwoFactorChallengeRecord>().HasQueryFilter(e => !e.IsDeleted);
    }
}
