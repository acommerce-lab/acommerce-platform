using ACommerce.SharedKernel.Abstractions.Repositories;
using Ashare.Api.Entities;
using Serilog;

namespace Ashare.Api.Services;

/// <summary>
/// بذر الإشعارات لعشير الإصدار الأول — لضمان وجود محتوى في
/// /notifications عند فتح التطبيق لأول مرة. الرسائل تحاكي تدفّق عشير
/// الفعلي (حجوزات، رسائل، عروض جديدة، ترقيات اشتراك).
/// </summary>
internal static class AshareNotificationsSeed
{
    public static async Task SeedAsync(IRepositoryFactory factory, CancellationToken ct)
    {
        var repo = factory.CreateRepository<Notification>();
        var existing = await repo.ListAllAsync(ct);
        if (existing.Count > 0)
        {
            Log.Debug("AshareNotificationsSeed: already seeded ({Count} records)", existing.Count);
            return;
        }

        var now = DateTime.UtcNow;
        var recipients = new[] { AshareSeeder.UserIds.OwnerAhmed, AshareSeeder.UserIds.CustomerSara };

        foreach (var user in recipients)
        {
            foreach (var template in BuildTemplates())
            {
                await repo.AddAsync(new Notification
                {
                    Id = Guid.NewGuid(),
                    CreatedAt = now.AddHours(-template.HoursAgo),
                    UserId = user,
                    Title = template.Title,
                    Body = template.Body,
                    Type = template.Type,
                    Priority = template.Priority,
                    Channel = "inapp",
                    IsRead = template.IsRead,
                    ReadAt = template.IsRead ? now.AddHours(-template.HoursAgo + 1) : null,
                    SentAt = now.AddHours(-template.HoursAgo),
                    DeliveryStatus = "sent"
                }, ct);
            }
        }

        Log.Information("AshareNotificationsSeed: seeded notifications for {Count} users", recipients.Length);
    }

    private static IEnumerable<NotificationTemplate> BuildTemplates()
    {
        yield return new("booking", "طلب الحجز قيد المراجعة", "مالك العرض سيتواصل خلال 24 ساعة",          HoursAgo:   2, IsRead: false, Priority: "high");
        yield return new("booking", "تمّ تأكيد حجزك",          "شقّة حيّ النرجس — من 1 مايو 2026",            HoursAgo:  10, IsRead: false, Priority: "high");
        yield return new("message", "رسالة جديدة من المالك",   "مرحباً، المفتاح جاهز الأسبوع القادم",         HoursAgo:  26, IsRead: true,  Priority: "normal");
        yield return new("info",    "عرض جديد قريب منك",        "استديو الدرعية — 1800 ر.س / شهر",            HoursAgo:  30, IsRead: true,  Priority: "normal");
        yield return new("info",    "خصم 10% على الاشتراك",     "كن مُضيفاً مُعتَمَداً بخصم لفترة محدودة",     HoursAgo:  48, IsRead: true,  Priority: "low");
        yield return new("system",  "تقييم جديد",               "تمّ إضافة تقييم 5 نجوم لمشاركتك",           HoursAgo:  72, IsRead: true,  Priority: "normal");
        yield return new("system",  "تحديث سياسة الخصوصيّة",    "اطّلع على النصّ الجديد قبل 1 مايو",          HoursAgo: 120, IsRead: true,  Priority: "low");
    }

    private sealed record NotificationTemplate(
        string Type, string Title, string Body,
        int HoursAgo, bool IsRead, string Priority);
}
