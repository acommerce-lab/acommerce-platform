using System.ComponentModel.DataAnnotations;
using ACommerce.Kits.DynamicAttributes.Operations;
using ACommerce.Kits.Listings.Domain;
using ACommerce.Kits.Profiles.Operations;
using ACommerce.SharedKernel.Domain.Entities;

namespace Ejar.Api.Data;

// EF entities — كيانات قاعدة البيانات ترث من IBaseEntity (Guid Id, CreatedAt, UpdatedAt, IsDeleted).
// متوافقة مع BaseAsyncRepository<T> من SharedKernel.
//
// UserEntity + ListingEntity يُنَفِّذان interfaces الكيتس (IUserProfile/IListing)
// + IHasDynamicAttributes لِيَدعَما كيت <see cref="ACommerce.Kits.DynamicAttributes"/>.
// نَفس النَّمَط المُطَبَّق في Ashare V3.

public sealed class UserEntity : IBaseEntity, IUserProfile, IHasDynamicAttributes
{
    [Key] public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    [MaxLength(120)] public string FullName { get; set; } = "";
    [MaxLength(20)]  public string Phone { get; set; } = "";
    public bool PhoneVerified { get; set; }
    [MaxLength(120)] public string Email { get; set; } = "";
    public bool EmailVerified { get; set; }
    [MaxLength(60)]  public string City { get; set; } = "";
    public DateTime MemberSince { get; set; }
    [MaxLength(2000)] public string? AvatarUrl { get; set; }

    /// <summary>JSON snapshot لِسِمات ديناميكِيَّة لِلبروفايل (Bio, Occupation, …).</summary>
    public string? AttributesJson { get; set; }

    // ─── IUserProfile explicit (Id يَنحَوَّل لِنَصّ) ──
    string  IUserProfile.Id            => Id.ToString();
    string  IUserProfile.FullName      => FullName;
    string  IUserProfile.Phone         => Phone;
    bool    IUserProfile.PhoneVerified => PhoneVerified;
    string? IUserProfile.Email         => Email;
    bool    IUserProfile.EmailVerified => EmailVerified;
    string  IUserProfile.City          => City;
    string? IUserProfile.AvatarUrl     => AvatarUrl;
    DateTime IUserProfile.MemberSince  => MemberSince;

    // ─── IHasDynamicAttributes — sentinel ثابِت لِنِطاق البروفايل ──
    Guid? IHasDynamicAttributes.DynamicAttributeScopeId =>
        new Guid("00000000-0000-0000-0000-00000E1A0F01");  // = EjarProfileAttributes.ScopeId
}

public sealed class ListingEntity : IBaseEntity, IListing, IHasDynamicAttributes
{
    [Key] public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    [MaxLength(200)] public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public decimal Price { get; set; }
    [MaxLength(20)]  public string TimeUnit { get; set; } = "";
    [MaxLength(40)]  public string PropertyType { get; set; } = "";
    [MaxLength(60)]  public string City { get; set; } = "";
    [MaxLength(120)] public string District { get; set; } = "";
    public double Lat { get; set; }
    public double Lng { get; set; }
    [MaxLength(500)] public string AmenitiesCsv { get; set; } = "";
    public Guid OwnerId { get; set; }
    public int BedroomCount { get; set; }
    public int BathroomCount { get; set; }
    public int AreaSqm { get; set; }
    public bool IsVerified { get; set; }
    public int ViewsCount { get; set; }
    public int Status { get; set; } = 1;
    // بدون MaxLength → nvarchar(max). الصور data URLs (base64)، صورة واحدة قد
    // تتجاوز ٢٠٠٠ حرف. التحديد القديم كان يُفجِّر INSERT بـ "String would be
    // truncated" من SQL Server.
    public string ImagesCsv { get; set; } = "";

    // مُصغّر الصورة الرئيسيّة (~30KB JPEG، 400×400 max). يُولِّده الواجهة قبل
    // الرفع عبر canvas في image-compressor.js. تستهلكه استجابات /home/explore
    // و /favorites و /my-listings كحقل firstImage بدل حشو الصورة الكاملة في
    // قائمة بـ 60 إعلاناً (وفّر ~50MB لكلّ تحميل قائمة على هاتف).
    public string? ThumbnailUrl { get; set; }

    /// <summary>JSON snapshot لِسِمات ديناميكِيَّة لِلإعلان (مَفاتيح القالَب
    /// المُلتَقَطَة بِـ scope = PropertyType slug → قِيَم).</summary>
    public string? AttributesJson { get; set; }

    // ─── IListing explicit (Ejar names ↔ kit names) ──
    string IListing.Id              => Id.ToString();
    string IListing.OwnerId         => OwnerId.ToString();
    string IListing.Title           => Title;
    string IListing.Description     => Description;
    decimal IListing.Price          => Price;
    string IListing.TimeUnit        => string.IsNullOrEmpty(TimeUnit) ? "monthly" : TimeUnit;
    string IListing.PropertyType    => PropertyType;
    string IListing.City            => City;
    string IListing.District        => District;
    double IListing.Lat             => Lat;
    double IListing.Lng             => Lng;
    int IListing.BedroomCount       => BedroomCount;
    int IListing.BathroomCount      => BathroomCount;
    int IListing.AreaSqm            => AreaSqm;
    int IListing.Status             => Status;
    int IListing.ViewsCount         => ViewsCount;
    bool IListing.IsVerified        => IsVerified;
    string? IListing.ThumbnailUrl   => ThumbnailUrl;
    IReadOnlyList<string> IListing.Images =>
        string.IsNullOrEmpty(ImagesCsv)
            ? Array.Empty<string>()
            : ImagesCsv.Split('|', StringSplitOptions.RemoveEmptyEntries);
    IReadOnlyList<string> IListing.Amenities =>
        string.IsNullOrEmpty(AmenitiesCsv)
            ? Array.Empty<string>()
            : AmenitiesCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    DateTime IListing.CreatedAt     => CreatedAt;
    DateTime? IListing.UpdatedAt    => UpdatedAt;

    // ─── IHasDynamicAttributes — نِطاق القالَب = PropertyType slug ──
    // لا CategoryId مُستَقِلّ في Ejar؛ نَستَخدِم النَّوع كَ scope identifier
    // بِتَحويله إلى Guid مُشتَقّ (Deterministic) لِيَكون مَفتاحاً ثابِتاً
    // عَبر إعادات التَّشغيل.
    Guid? IHasDynamicAttributes.DynamicAttributeScopeId =>
        string.IsNullOrEmpty(PropertyType)
            ? null
            : EjarListingScopes.DeriveScopeId(PropertyType);
}

/// <summary>أَدوات اشتِقاق Scope GUIDs ثابِتَة لِفِئات Ejar (لا CategoryId
/// مُستَقِلّ كَما في Ashare).</summary>
public static class EjarListingScopes
{
    /// <summary>يُحَوِّل slug إلى Guid ثابِت (Deterministic) عَبر MD5.</summary>
    public static Guid DeriveScopeId(string slug)
    {
        using var md5 = System.Security.Cryptography.MD5.Create();
        var hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes("ejar-listing:" + slug.ToLowerInvariant()));
        return new Guid(hash);
    }
}

public sealed class ConversationEntity : IBaseEntity
{
    [Key] public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public Guid OwnerId { get; set; }
    [MaxLength(120)] public string PartnerName { get; set; } = "";
    public Guid PartnerId { get; set; }
    public Guid ListingId { get; set; }
    [MaxLength(200)] public string Subject { get; set; } = "";
    public DateTime LastAt { get; set; }

    /// <summary>عَدّاد قديم مُشتَرَك — لا يَستَخدِمه أحد بَعد. حلّ مَحلّه
    /// <see cref="OwnerUnread"/> + <see cref="PartnerUnread"/>. يَبقى كعمود
    /// لِيَنجح مَسار الـ EF بدون drop.</summary>
    public int UnreadCount { get; set; }

    /// <summary>غير مَقروءة بنظر الـ Owner (= رسائل الـ Partner منذ آخر قراءة).</summary>
    public int OwnerUnread   { get; set; }
    /// <summary>غير مَقروءة بنظر الـ Partner (= رسائل الـ Owner منذ آخر قراءة).</summary>
    public int PartnerUnread { get; set; }

    public List<MessageEntity> Messages { get; set; } = new();
}

public sealed class MessageEntity : IBaseEntity
{
    [Key] public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public Guid ConversationId { get; set; }
    [MaxLength(64)]  public string From { get; set; } = "";
    public string Text { get; set; } = "";
    public DateTime SentAt { get; set; }
}

public sealed class NotificationEntity : IBaseEntity
{
    [Key] public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public Guid UserId { get; set; }

    [MaxLength(200)] public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public bool IsRead { get; set; }
    [MaxLength(64)] public string? RelatedId { get; set; }
    [MaxLength(40)] public string Type { get; set; } = "system";
}

/// <summary>
/// رمز جهاز للـ Firebase Cloud Messaging (web push / mobile). يُسجَّل من
/// الواجهة عبر <c>POST /me/push-subscription</c> بعد ما يحصل المتصفّح/التطبيق
/// على الرمز من Firebase SDK. الباك يبثّ إشعارات للمستخدم على كلّ الرموز
/// المسجّلة لأجهزته (web + Android + iOS).
///
/// <para>تُستهلك من <see cref="ACommerce.Notification.Providers.Firebase.Storage.IDeviceTokenStore"/>
/// عبر <c>EjarDeviceTokenStore</c>. الرموز قد تنتهي صلاحيّتها — Firebase
/// Admin SDK تُرجع <c>NotRegistered</c> فيُحذف الصفّ المعنيّ تلقائياً.</para>
/// </summary>
public sealed class UserPushTokenEntity : IBaseEntity
{
    [Key] public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public Guid UserId { get; set; }

    /// <summary>رمز FCM الفريد للجهاز. حتى ٤ آلاف حرف نظرياً، نتركه nvarchar(max).</summary>
    public string Token { get; set; } = "";

    /// <summary>"web" / "android" / "ios" — لتشخيص أيّ منصّة فقط.</summary>
    [MaxLength(20)] public string? Platform { get; set; }
}


public sealed class PlanEntity : IBaseEntity
{
    [Key] public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    [MaxLength(60)]  public string Label { get; set; } = "";
    public decimal Price { get; set; }
    [MaxLength(20)]  public string CycleLabel { get; set; } = "";
    public int MaxActiveListings { get; set; }
    public int MaxFeaturedListings { get; set; }
    public int MaxImagesPerListing { get; set; }
    public bool IsRecommended { get; set; }
    public string Description { get; set; } = "";
    [MaxLength(2000)] public string FeaturesCsv { get; set; } = "";
}


// ─── Subscription + Invoice entities ─────────────────────────────────────
public sealed class SubscriptionEntity : IBaseEntity
{
    [Key] public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public Guid UserId { get; set; }
    public Guid PlanId { get; set; }
    [MaxLength(60)] public string PlanName { get; set; } = "";
    [MaxLength(20)] public string Status { get; set; } = "active";
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int ListingsLimit { get; set; }
    public int FeaturedLimit { get; set; }
    public int ImagesPerListing { get; set; }
}

public sealed class InvoiceEntity : IBaseEntity
{
    [Key] public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public Guid UserId { get; set; }
    public Guid PlanId { get; set; }
    public decimal Amount { get; set; }
    public DateTime Date { get; set; }
    [MaxLength(20)] public string Status { get; set; } = "paid";
}


public sealed class AppVersionEntity : IBaseEntity
{
    [Key] public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    [MaxLength(20)] public string Platform { get; set; } = "";   // web | wasm | mobile | admin
    [MaxLength(40)] public string Version  { get; set; } = "";   // semver-lite, e.g. "1.0.0"
    public int Status { get; set; }                              // VersionStatus enum
    public DateTime? SunsetAt { get; set; }
    public string? Notes { get; set; }
    public string? DownloadUrl { get; set; }
}

/// <summary>
/// رسالة في تذكرة دعم — مَعزولة عن جدول رسائل الدردشة. تذاكر الدعم
/// لها قناة تَخزين مُستقلّة فلا تَتسرّب إلى /conversations ولا تُحَفِّز
/// chat-side interceptors (FCM، broadcast، persistent-notif).
/// </summary>
public sealed class SupportMessageEntity : IBaseEntity
{
    [Key] public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public Guid TicketId { get; set; }
    /// <summary>"User:GUID" أو "System".</summary>
    [MaxLength(64)] public string SenderPartyId { get; set; } = "";
    public string Body { get; set; } = "";
    public DateTime SentAt { get; set; }
}
