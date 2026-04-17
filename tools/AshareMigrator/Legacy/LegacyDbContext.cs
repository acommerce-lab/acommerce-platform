using Microsoft.EntityFrameworkCore;

namespace AshareMigrator.Legacy;

public class LegacyDbContext : DbContext
{
    public LegacyDbContext(DbContextOptions<LegacyDbContext> options) : base(options) { }

    public DbSet<LegacyUser> Users => Set<LegacyUser>();
    public DbSet<LegacyVendor> Vendors => Set<LegacyVendor>();
    public DbSet<LegacyCategory> ProductCategories => Set<LegacyCategory>();
    public DbSet<LegacyListing> ProductListings => Set<LegacyListing>();
    public DbSet<LegacyBooking> Bookings => Set<LegacyBooking>();
    public DbSet<LegacySubscriptionPlan> SubscriptionPlans => Set<LegacySubscriptionPlan>();
    public DbSet<LegacySubscription> Subscriptions => Set<LegacySubscription>();
    public DbSet<LegacyProfile> Profiles => Set<LegacyProfile>();

    protected override void OnModelCreating(ModelBuilder m)
    {
        m.Entity<LegacyUser>().ToTable("Users");
        m.Entity<LegacyVendor>().ToTable("Vendors");
        m.Entity<LegacyCategory>().ToTable("ProductCategories");
        m.Entity<LegacyListing>().ToTable("ProductListings");
        m.Entity<LegacyBooking>().ToTable("Bookings");
        m.Entity<LegacySubscriptionPlan>().ToTable("SubscriptionPlans");
        m.Entity<LegacySubscription>().ToTable("Subscriptions");
        m.Entity<LegacyProfile>().ToTable("Profiles");

        m.Entity<LegacyUser>().HasQueryFilter(e => !e.IsDeleted);
        m.Entity<LegacyVendor>().HasQueryFilter(e => !e.IsDeleted);
        m.Entity<LegacyCategory>().HasQueryFilter(e => !e.IsDeleted);
        m.Entity<LegacyListing>().HasQueryFilter(e => !e.IsDeleted);
        m.Entity<LegacyBooking>().HasQueryFilter(e => !e.IsDeleted);
        m.Entity<LegacySubscriptionPlan>().HasQueryFilter(e => !e.IsDeleted);
        m.Entity<LegacySubscription>().HasQueryFilter(e => !e.IsDeleted);
        m.Entity<LegacyProfile>().HasQueryFilter(e => !e.IsDeleted);
    }
}
