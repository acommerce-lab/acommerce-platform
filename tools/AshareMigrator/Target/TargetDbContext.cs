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
        m.Entity<NewUser>().ToTable("Users");
        m.Entity<NewCategory>().ToTable("Categories");
        m.Entity<NewListing>().ToTable("Listings");
        m.Entity<NewBooking>().ToTable("Bookings");
        m.Entity<NewPlan>().ToTable("Plans");
        m.Entity<NewSubscription>().ToTable("Subscriptions");
        m.Entity<NewProfile>().ToTable("Profiles");

        m.Entity<NewUser>().HasQueryFilter(e => !e.IsDeleted);
        m.Entity<NewCategory>().HasQueryFilter(e => !e.IsDeleted);
        m.Entity<NewListing>().HasQueryFilter(e => !e.IsDeleted);
        m.Entity<NewBooking>().HasQueryFilter(e => !e.IsDeleted);
        m.Entity<NewPlan>().HasQueryFilter(e => !e.IsDeleted);
        m.Entity<NewSubscription>().HasQueryFilter(e => !e.IsDeleted);
        m.Entity<NewProfile>().HasQueryFilter(e => !e.IsDeleted);
    }
}
