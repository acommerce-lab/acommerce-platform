using Ashare.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Ashare.Admin.Api.Data;

/// <summary>
/// DbContext لوحة الإدارة - يشمل نفس مجموعات الكيانات من Customer API
/// حتى يمكن للوحة الإدارة قراءة وتعديل نفس البيانات.
/// </summary>
public class AdminDbContext : DbContext
{
    public AdminDbContext(DbContextOptions<AdminDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Listing> Listings => Set<Listing>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<DeviceToken> DeviceTokens => Set<DeviceToken>();
    public DbSet<TwoFactorChallengeRecord> TwoFactorChallengeRecords => Set<TwoFactorChallengeRecord>();
    public DbSet<Profile> Profiles => Set<Profile>();
    public DbSet<MediaFile> MediaFiles => Set<MediaFile>();
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Soft delete query filter: استبعاد المحذوفات بشكل افتراضي
        modelBuilder.Entity<User>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Category>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Listing>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Booking>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Payment>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Notification>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Conversation>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Message>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<DeviceToken>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<TwoFactorChallengeRecord>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Profile>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<MediaFile>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Plan>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Subscription>().HasQueryFilter(e => !e.IsDeleted);
    }
}
