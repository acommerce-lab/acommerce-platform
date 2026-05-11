using ACommerce.Kits.Discovery.Domain;
using Ashare.V3.Domain;
using Microsoft.EntityFrameworkCore;

namespace Ashare.V3.Data;

/// <summary>
/// DbContext لِـ Ashare V3. كُلّ <c>DbSet</c> مُعَيَّن بِـ <c>ToTable(...)</c>
/// عَلى **اسم الجَدول الفِعليّ في asharedb** — لا تُغَيِّر هذه الأَسماء، الـ
/// production DB يَعتَمِد عَليها.
///
/// <para><b>قَواعِد سَلامَة بَيانات</b>:
/// <list type="number">
///   <item>لا migrations تَكتُب فَوق الجَداوِل القائِمَة. Migration الوَحيد
///         (<c>InitialNewTables</c>) يُنشِئ ثَلاث جَداوِل جَديدَة فَقَط:
///         <c>Favorites</c>, <c>Reports</c>, <c>Notifications</c>.</item>
///   <item>الـ Bootstrap لا يَستَدعي <c>Database.Migrate()</c> الكامِل —
///         فَقَط يَفحَص وُجود الجَداوِل الجَديدَة و يُنشِئها لَو ناقِصَة.</item>
///   <item>لا <c>EnsureCreated()</c> ولا <c>EnsureDeleted()</c> في الـ
///         production code path.</item>
/// </list></para>
/// </summary>
public sealed class AshareV3DbContext : DbContext
{
    public AshareV3DbContext(DbContextOptions<AshareV3DbContext> opts) : base(opts) { }

    // ── existing asharedb tables ─────────────────────────────────────────
    public DbSet<ProfileEntity>                Profiles          => Set<ProfileEntity>();
    public DbSet<ProductEntity>                Products          => Set<ProductEntity>();
    public DbSet<ProductCategoryEntity>        ProductCategories => Set<ProductCategoryEntity>();
    public DbSet<ProductListingEntity>         ProductListings   => Set<ProductListingEntity>();
    public DbSet<ChatEntity>                   Chats             => Set<ChatEntity>();
    public DbSet<ChatParticipantEntity>        ChatParticipants  => Set<ChatParticipantEntity>();
    public DbSet<MessageEntity>                Messages          => Set<MessageEntity>();
    public DbSet<MessageReadEntity>            MessageReads      => Set<MessageReadEntity>();
    public DbSet<ComplaintEntity>              Complaints        => Set<ComplaintEntity>();
    public DbSet<ComplaintReplyEntity>         ComplaintReplies  => Set<ComplaintReplyEntity>();
    public DbSet<BookingEntity>                Bookings          => Set<BookingEntity>();
    public DbSet<BookingStatusHistoryEntity>   BookingHistory    => Set<BookingStatusHistoryEntity>();
    public DbSet<DeviceTokenEntity>            DeviceTokens      => Set<DeviceTokenEntity>();
    public DbSet<AppVersionEntity>             AppVersions       => Set<AppVersionEntity>();
    public DbSet<LegalPageEntity>              LegalPages        => Set<LegalPageEntity>();

    // ── new tables (created by additive Bootstrap CREATE TABLE) ─────────
    public DbSet<FavoriteEntity>      Favorites          => Set<FavoriteEntity>();
    public DbSet<ReportEntity>        Reports            => Set<ReportEntity>();
    public DbSet<NotificationEntity>  Notifications      => Set<NotificationEntity>();
    public DbSet<DiscoveryCategory>   DiscoveryCategories => Set<DiscoveryCategory>();
    public DbSet<DiscoveryRegion>     DiscoveryRegions    => Set<DiscoveryRegion>();
    public DbSet<DiscoveryAmenity>    DiscoveryAmenities  => Set<DiscoveryAmenity>();

    // ── Locations (asharedb existing — Countries/Regions/Cities/Neighborhoods)
    public DbSet<CountryEntity>       Countries          => Set<CountryEntity>();
    public DbSet<RegionEntity>        Regions            => Set<RegionEntity>();
    public DbSet<CityEntity>          Cities             => Set<CityEntity>();
    public DbSet<NeighborhoodEntity>  Neighborhoods      => Set<NeighborhoodEntity>();

    // ── Category attribute templates (V3-additive, hybrid code+DB)
    public DbSet<CategoryAttributeTemplateEntity> CategoryAttributeTemplates =>
        Set<CategoryAttributeTemplateEntity>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // ─── أَسماء جَداوِل asharedb (singular pattern V2) ────────────
        b.Entity<ProfileEntity>().ToTable("Profile");
        b.Entity<ProductEntity>().ToTable("Products");
        b.Entity<ProductCategoryEntity>().ToTable("ProductCategory");
        b.Entity<ProductListingEntity>().ToTable("ProductListing");
        b.Entity<ChatEntity>().ToTable("Chat");
        b.Entity<ChatParticipantEntity>().ToTable("ChatParticipant");
        b.Entity<MessageEntity>().ToTable("Message");
        b.Entity<MessageReadEntity>().ToTable("MessageRead");
        b.Entity<ComplaintEntity>().ToTable("Complaint");
        b.Entity<ComplaintReplyEntity>().ToTable("ComplaintReply");
        b.Entity<BookingEntity>().ToTable("Booking");
        b.Entity<BookingStatusHistoryEntity>().ToTable("BookingStatusHistory");
        b.Entity<DeviceTokenEntity>().ToTable("DeviceTokens");
        b.Entity<AppVersionEntity>().ToTable("AppVersions");
        b.Entity<LegalPageEntity>().ToTable("LegalPage");

        // ─── جَداوِل جَديدَة لِـ V3 (plural، حُرٌ بِها) ─────────────
        b.Entity<FavoriteEntity>().ToTable("Favorites");
        b.Entity<ReportEntity>().ToTable("Reports");
        b.Entity<NotificationEntity>().ToTable("Notifications");
        b.Entity<DiscoveryCategory>().ToTable("DiscoveryCategories");
        b.Entity<DiscoveryRegion>().ToTable("DiscoveryRegions");
        b.Entity<DiscoveryAmenity>().ToTable("DiscoveryAmenities");
        b.Entity<DiscoveryCategory>().HasQueryFilter(e => !e.IsDeleted);
        b.Entity<DiscoveryRegion>().HasQueryFilter(e => !e.IsDeleted);
        b.Entity<DiscoveryAmenity>().HasQueryFilter(e => !e.IsDeleted);

        // ─── Locations جَداوِل asharedb القائِمَة (plural names) ─────
        b.Entity<CountryEntity>().ToTable("Countries").HasQueryFilter(e => !e.IsDeleted);
        b.Entity<RegionEntity>().ToTable("Regions").HasQueryFilter(e => !e.IsDeleted);
        b.Entity<CityEntity>().ToTable("Cities").HasQueryFilter(e => !e.IsDeleted);
        b.Entity<NeighborhoodEntity>().ToTable("Neighborhoods").HasQueryFilter(e => !e.IsDeleted);

        // CategoryAttributeTemplates — V3-only، plural، يُنشَأ بِـ EnsureCreated.
        b.Entity<CategoryAttributeTemplateEntity>().ToTable("CategoryAttributeTemplates")
            .HasQueryFilter(e => !e.IsDeleted);
        b.Entity<CategoryAttributeTemplateEntity>().HasIndex(e => e.CategorySlug).IsUnique();

        // ─── soft-delete global query filter (مُتَّسِق مَع V2 pattern) ─
        b.Entity<ProfileEntity>().HasQueryFilter(e => !e.IsDeleted);
        b.Entity<ProductEntity>().HasQueryFilter(e => !e.IsDeleted);
        b.Entity<ProductListingEntity>().HasQueryFilter(e => !e.IsDeleted);
        b.Entity<ProductCategoryEntity>().HasQueryFilter(e => !e.IsDeleted);
        b.Entity<ChatEntity>().HasQueryFilter(e => !e.IsDeleted);
        b.Entity<ChatParticipantEntity>().HasQueryFilter(e => !e.IsDeleted);
        b.Entity<MessageEntity>().HasQueryFilter(e => !e.IsDeleted);
        b.Entity<MessageReadEntity>().HasQueryFilter(e => !e.IsDeleted);
        b.Entity<ComplaintEntity>().HasQueryFilter(e => !e.IsDeleted);
        b.Entity<ComplaintReplyEntity>().HasQueryFilter(e => !e.IsDeleted);
        b.Entity<BookingEntity>().HasQueryFilter(e => !e.IsDeleted);
        b.Entity<FavoriteEntity>().HasQueryFilter(e => !e.IsDeleted);
        b.Entity<ReportEntity>().HasQueryFilter(e => !e.IsDeleted);
        b.Entity<NotificationEntity>().HasQueryFilter(e => !e.IsDeleted);

        // ─── PKs (Guid) + بَعض الفَهارِس ────────────────────────────
        b.Entity<FavoriteEntity>().HasIndex(f => new { f.UserId, f.ListingId }).IsUnique();
        b.Entity<NotificationEntity>().HasIndex(n => new { n.UserId, n.IsRead });
        b.Entity<ReportEntity>().HasIndex(r => new { r.EntityType, r.EntityId });

        // ─── decimal precision (asharedb default decimal(18,2)) ──────
        // بِدون هذا EF يُحَذِّر مِن truncation. نَستَخدِم نَفس precision/scale
        // المَوجودَين في asharedb (تَفقَّدنا الأَعمِدَة في sys.columns).
        b.Entity<BookingEntity>().Property(e => e.TotalPrice).HasPrecision(18, 2);
        b.Entity<BookingEntity>().Property(e => e.DepositPercentage).HasPrecision(5, 2);
        b.Entity<BookingEntity>().Property(e => e.DepositAmount).HasPrecision(18, 2);
        b.Entity<BookingEntity>().Property(e => e.EscrowReleasedAmount).HasPrecision(18, 2);
        b.Entity<ProductEntity>().Property(e => e.Weight).HasPrecision(10, 3);
        b.Entity<ProductEntity>().Property(e => e.Length).HasPrecision(10, 3);
        b.Entity<ProductEntity>().Property(e => e.Width).HasPrecision(10, 3);
        b.Entity<ProductEntity>().Property(e => e.Height).HasPrecision(10, 3);
        b.Entity<ProductListingEntity>().Property(e => e.Price).HasPrecision(18, 2);
        b.Entity<ProductListingEntity>().Property(e => e.CompareAtPrice).HasPrecision(18, 2);
        b.Entity<ProductListingEntity>().Property(e => e.Cost).HasPrecision(18, 2);
        b.Entity<ProductListingEntity>().Property(e => e.Rating).HasPrecision(3, 2);
        b.Entity<ProductListingEntity>().Property(e => e.CommissionPercentage).HasPrecision(5, 2);

        // ─── Chat ⇄ ChatParticipant navigation ───────────────────────
        b.Entity<ChatEntity>()
            .HasMany(c => c.Participants)
            .WithOne()
            .HasForeignKey(p => p.ChatId);

        // ─── حُقول الـ explicit interface members لا تُحفَظ ─────────
        // (EF يَتَجاهَل المُلكيّات الـ explicit interface تلقائيّاً
        // لأَنَّها لَيسَت public properties — لا حاجة لِـ [NotMapped]).
    }
}
