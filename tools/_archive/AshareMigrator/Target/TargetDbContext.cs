using Microsoft.EntityFrameworkCore;

namespace AshareMigrator.Target;

public class TargetDbContext : DbContext
{
    public TargetDbContext(DbContextOptions<TargetDbContext> options) : base(options) { }

    public DbSet<NewUser> Users => Set<NewUser>();
    public DbSet<NewCategory> Categories => Set<NewCategory>();
    public DbSet<NewListing> Listings => Set<NewListing>();
    public DbSet<NewBooking> Bookings => Set<NewBooking>();
    public DbSet<NewPlan> Plans => Set<NewPlan>();
    public DbSet<NewSubscription> Subscriptions => Set<NewSubscription>();
    public DbSet<NewProfile> Profiles => Set<NewProfile>();

    protected override void OnModelCreating(ModelBuilder m)
    {
        // Ashare.Api uses modelBuilder.Entity(type) without ToTable — EF Core
        // defaults to the class name (singular). Must match exactly.
        m.Entity<NewUser>().ToTable("User");
        m.Entity<NewCategory>().ToTable("Category");
        m.Entity<NewListing>().ToTable("Listing");
        m.Entity<NewBooking>().ToTable("Booking");
        m.Entity<NewPlan>().ToTable("Plan");
        m.Entity<NewSubscription>().ToTable("Subscription");
        m.Entity<NewProfile>().ToTable("Profile");

        m.Entity<NewUser>().HasQueryFilter(e => !e.IsDeleted);
        m.Entity<NewCategory>().HasQueryFilter(e => !e.IsDeleted);
        m.Entity<NewListing>().HasQueryFilter(e => !e.IsDeleted);
        m.Entity<NewBooking>().HasQueryFilter(e => !e.IsDeleted);
        m.Entity<NewPlan>().HasQueryFilter(e => !e.IsDeleted);
        m.Entity<NewSubscription>().HasQueryFilter(e => !e.IsDeleted);
        m.Entity<NewProfile>().HasQueryFilter(e => !e.IsDeleted);
    }
}
