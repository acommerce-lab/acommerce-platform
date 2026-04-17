using Microsoft.EntityFrameworkCore;

namespace AshareMigrator.Legacy;

public class LegacyDbContext : DbContext
{
    public LegacyDbContext(DbContextOptions<LegacyDbContext> options) : base(options) { }

    // لا يوجد جدول Users في قاعدة المصدر — المستخدمون يُبنى من Profile.UserId
    public DbSet<LegacyVendor> Vendors => Set<LegacyVendor>();
    public DbSet<LegacyCategory> Categories => Set<LegacyCategory>();
    public DbSet<LegacyListing> Listings => Set<LegacyListing>();
    public DbSet<LegacyBooking> Bookings => Set<LegacyBooking>();
    public DbSet<LegacySubscriptionPlan> SubscriptionPlans => Set<LegacySubscriptionPlan>();
    public DbSet<LegacySubscription> Subscriptions => Set<LegacySubscription>();
    public DbSet<LegacyProfile> Profiles => Set<LegacyProfile>();

    protected override void OnModelCreating(ModelBuilder m)
    {
        m.Entity<LegacyVendor>().ToTable("Vendor");
        m.Entity<LegacyCategory>().ToTable("ProductCategory");
        m.Entity<LegacyListing>().ToTable("ProductListing");
        m.Entity<LegacyBooking>().ToTable("Booking");
        m.Entity<LegacySubscriptionPlan>().ToTable("SubscriptionPlans");
        m.Entity<LegacySubscription>().ToTable("Subscriptions");
        m.Entity<LegacyProfile>().ToTable("Profile");

        // بدون قيود حذف ناعم — نقرأ كل البيانات كما هي
    }
}
