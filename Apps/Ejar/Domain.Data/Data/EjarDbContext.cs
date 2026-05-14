using Microsoft.EntityFrameworkCore;
using ACommerce.Kits.Support.Domain;
using ACommerce.Kits.Reports.Domain;
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
    public DbSet<UserPushTokenEntity> UserPushTokens => Set<UserPushTokenEntity>();
    public DbSet<Favorite>            Favorites      => Set<Favorite>();
    public DbSet<PlanEntity>          Plans          => Set<PlanEntity>();
    // تذاكر الدعم (Support kit) — مَعزولة عن Chat kit. الردود الآن في
    // SupportMessages الخاصّ بالدعم، لا في جدول الدردشة.
    public DbSet<SupportTicket>        SupportTickets  => Set<SupportTicket>();
    public DbSet<SupportMessageEntity> SupportMessages => Set<SupportMessageEntity>();
    public DbSet<ReportEntity>        Reports        => Set<ReportEntity>();
    public DbSet<SubscriptionEntity>  Subscriptions  => Set<SubscriptionEntity>();
    public DbSet<InvoiceEntity>       Invoices       => Set<InvoiceEntity>();
    public DbSet<AppVersionEntity>    AppVersions    => Set<AppVersionEntity>();
    
    // Discovery Kit
    public DbSet<DiscoveryCategory>   DiscoveryCategories => Set<DiscoveryCategory>();
    public DbSet<DiscoveryRegion>     DiscoveryRegions    => Set<DiscoveryRegion>();
    public DbSet<DiscoveryAmenity>    DiscoveryAmenities   => Set<DiscoveryAmenity>();

    // Taxonomy kit — جَدول واحِد لِكُلّ شَجَرَات التَطبيق (مُمَيَّزَة بِـ RootCode)
    public DbSet<TaxonomyNodeEntity>  TaxonomyNodes       => Set<TaxonomyNodeEntity>();

    // DynamicAttributes — مَخطَط كاتالوج: تَعريف + قِيَم + رِبط بِفِئَة
    // (slug-derived scopeId). الـ EjarAttributeTemplateSource يَقرَأ مِن هُنا
    // بَدَل constants لِيَمنَح لوحَة الإدارَة سَيطَرَة كامِلَة.
    public DbSet<AttributeDefinitionEntity>      AttributeDefinitions      => Set<AttributeDefinitionEntity>();
    public DbSet<AttributeValueEntity>           AttributeValues           => Set<AttributeValueEntity>();
    public DbSet<CategoryAttributeMappingEntity> CategoryAttributeMappings => Set<CategoryAttributeMappingEntity>();

    // Idempotency — سِجِلّ التَكرارات الَّتي يَفحَصها IdempotencyInterceptor
    public DbSet<OperationIdempotencyEntity> OperationIdempotency => Set<OperationIdempotencyEntity>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<ConversationEntity>()
            .HasMany(c => c.Messages)
            .WithOne()
            .HasForeignKey(m => m.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        // SupportTicket: index على UserId. ConversationId يَبقى عمودَاً
        // قديماً (لتوافق مع صفوف سابقة) لكنّ الكيت الآن يَستخدم
        // SupportMessages بدله — لا index ضروريّ.
        b.Entity<SupportTicket>().HasIndex(t => t.UserId);
        b.Entity<SupportMessageEntity>().HasIndex(m => m.TicketId);
        b.Entity<SupportMessageEntity>().HasQueryFilter(e => !e.IsDeleted);

        // Reports kit: index على ReporterId (للقائمة الشخصيّة) + EntityType+EntityId
        // (للبحث "كم بلاغاً على هذا الإعلان"). filter لاستبعاد المحذوفة.
        b.Entity<ReportEntity>().HasIndex(r => r.ReporterId);
        b.Entity<ReportEntity>().HasIndex(r => new { r.EntityType, r.EntityId });
        b.Entity<ReportEntity>().HasQueryFilter(e => !e.IsDeleted);

        b.Entity<ListingEntity>().HasIndex(l => l.City);
        b.Entity<ListingEntity>().HasIndex(l => l.PropertyType);
        b.Entity<ListingEntity>().HasIndex(l => l.OwnerId);
        b.Entity<UserEntity>().HasIndex(u => u.Phone).IsUnique();
        b.Entity<Favorite>().HasIndex(f => new { f.UserId, f.EntityType, f.EntityId }).IsUnique();
        b.Entity<MessageEntity>().HasIndex(m => m.ConversationId);
        b.Entity<ConversationEntity>().HasIndex(c => c.OwnerId);
        b.Entity<ConversationEntity>().HasIndex(c => c.PartnerId);
        b.Entity<AppVersionEntity>().HasIndex(v => new { v.Platform, v.Version }).IsUnique();
        b.Entity<AppVersionEntity>().HasQueryFilter(e => !e.IsDeleted);

        // Taxonomy: index رئيسي (RootCode, Code) لِـ slug lookup سَريع +
        // index ثانوي (RootCode, ParentId, SortOrder) لِبِناء الشَجَرَة.
        b.Entity<TaxonomyNodeEntity>().HasIndex(t => new { t.RootCode, t.Code }).IsUnique();
        b.Entity<TaxonomyNodeEntity>().HasIndex(t => new { t.RootCode, t.ParentId, t.SortOrder });
        b.Entity<TaxonomyNodeEntity>().HasQueryFilter(e => !e.IsDeleted);

        // OperationIdempotency: Key فَريد لِيَكون lookup عَلى string فَريد.
        b.Entity<OperationIdempotencyEntity>().HasIndex(o => o.Key).IsUnique();
        b.Entity<OperationIdempotencyEntity>().HasQueryFilter(e => !e.IsDeleted);

        // DynamicAttributes — مَخطَط كاتالوج. الـ Code فَريد عَلى مُستَوى الجَدول
        // لِيَكون lookup سَريع، الـ (CategoryId, AttributeDefinitionId) فَريد
        // لِمَنع تَكرار الـ mapping. الـ AttributeValues مَفهرَس بِالـ DefId
        // لِجَلب خِيارات select-like.
        b.Entity<AttributeDefinitionEntity>().HasIndex(d => d.Code).IsUnique();
        b.Entity<AttributeDefinitionEntity>().HasQueryFilter(e => !e.IsDeleted);
        b.Entity<AttributeValueEntity>().HasIndex(v => new { v.AttributeDefinitionId, v.Value }).IsUnique();
        b.Entity<AttributeValueEntity>().HasQueryFilter(e => !e.IsDeleted);
        b.Entity<CategoryAttributeMappingEntity>().HasIndex(m => new { m.CategoryId, m.AttributeDefinitionId }).IsUnique();
        b.Entity<CategoryAttributeMappingEntity>().HasIndex(m => m.CategoryId);
        b.Entity<CategoryAttributeMappingEntity>().HasQueryFilter(e => !e.IsDeleted);

        // Push tokens — index على UserId للبحث السريع، unique على Token وحده
        // (نفس رمز قد يصدُر لمستخدم بعد re-login على نفس الجهاز فنُحدّث UserId).
        b.Entity<UserPushTokenEntity>().HasIndex(t => t.UserId);
        b.Entity<UserPushTokenEntity>().HasIndex(t => t.Token).IsUnique();
        b.Entity<UserPushTokenEntity>().HasQueryFilter(e => !e.IsDeleted);

        // Decimal precision: SQL Server يستخدم decimal(18,2) افتراضياً مع تحذير.
        // نُحدّده صراحةً (10 منازل + 2 كسر) ليكفي ريال/دولار حتى ٩٩٬٩٩٩٬٩٩٩.٩٩.
        b.Entity<ListingEntity>().Property(l => l.Price).HasPrecision(12, 2);
        b.Entity<PlanEntity>()   .Property(p => p.Price).HasPrecision(12, 2);
        b.Entity<InvoiceEntity>().Property(i => i.Amount).HasPrecision(12, 2);

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
