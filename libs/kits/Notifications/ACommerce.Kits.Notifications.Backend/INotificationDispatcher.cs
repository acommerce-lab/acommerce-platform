using ACommerce.OperationEngine.Analyzers;
using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;
using ACommerce.OperationEngine.Wire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ACommerce.Kits.Notifications.Backend;

/// <summary>
/// واجهة إرسال الإشعارات كـ <b>عمليّات OAM</b> (لا استدعاء مباشر للـ store).
///
/// <para>المعترضات (Chat.WithNotifications، Booking.Notify، …) تحقن هذه
/// الواجهة بدل <see cref="INotificationStore"/>، فلا تعرف <i>كيف</i>
/// تُحفَظ الإشعارات (DB؟ Redis؟ in-memory؟) — تطلب فقط "أنشئ إشعاراً
/// لـ X" وينطلق envelope كامل: analyzers + parent linkage + SaveAtEnd.</para>
///
/// <para>الفائدة المحاسبيّة: إنشاء الإشعار صار حدثاً مرئيّاً للـ audit
/// log، يمكن عكسه (<c>.Reverses()</c>)، يمكن تتبّعه عبر parent → child
/// (Message.send → Notification.create)، ولا يحدث بدون تمرير على
/// كل interceptor مسجَّل على <c>notification.create</c>.</para>
/// </summary>
public interface INotificationDispatcher
{
    Task DispatchCreateAsync(
        string userId,
        string type,
        string title,
        string body,
        string? relatedId,
        Operation? parent,
        CancellationToken ct);
}

/// <summary>
/// التطبيق القياسيّ: يبني <c>notification.create</c> كقيد كامل ثمّ يُمرّره
/// لـ <see cref="OpEngine"/>. الـ Execute body يستدعي
/// <see cref="INotificationStore.AddNoSaveAsync"/> (tracker-only)، و
/// <c>.SaveAtEnd()</c> يُسلِّم الحفظ لـ <c>IUnitOfWork</c> ذرّيّاً.
/// </summary>
public sealed class OpEngineNotificationDispatcher : INotificationDispatcher
{
    private readonly OpEngine _engine;
    private readonly ILogger<OpEngineNotificationDispatcher> _log;

    public OpEngineNotificationDispatcher(
        OpEngine engine, ILogger<OpEngineNotificationDispatcher> log)
    { _engine = engine; _log = log; }

    public async Task DispatchCreateAsync(
        string userId, string type, string title, string body,
        string? relatedId, Operation? parent, CancellationToken ct)
    {
        var op = Entry.Create(NotificationOps.Create)
            .Describe($"Notify {type} → user:{userId}")
            .From("System:Notifications", 1, ("role", "issuer"))
            .To($"User:{userId}",          1, ("role", "recipient"))
            .Mark(NotificationMarkers.IsNotification)
            .Tag(NotificationTagKeys.UserId,    userId)
            .Tag(NotificationTagKeys.Type,      type)
            .Tag(NotificationTagKeys.Title,     title)
            .Tag(NotificationTagKeys.Body,      body)
            .Tag(NotificationTagKeys.RelatedId, relatedId ?? "")
            .Tag(NotificationTagKeys.ParentOpId, parent?.Id.ToString() ?? "")
            .Analyze(new RequiredFieldAnalyzer("user_id", () => userId))
            .Analyze(new RequiredFieldAnalyzer("type",    () => type))
            .Analyze(new RequiredFieldAnalyzer("title",   () => title))
            .Execute(async ctx =>
            {
                // الـ store اختياريّ: لو غاب (in-memory mode، لا جدول
                // إشعارات، …) العمليّة تنجح ويبقى الـ envelope صالحاً
                // كمصدر للأحداث الأخرى (push، broadcast). DB سينك إسقاطيّ.
                var store = ctx.Services.GetService<INotificationStore>();
                if (store is null)
                {
                    _log.LogDebug("Notifications: لا store مسجَّل — الإشعار حدث OAM فقط");
                    return;
                }
                var item = await store.AddNoSaveAsync(
                    userId, type, title, body, relatedId, ctx.CancellationToken);
                ctx.WithEntity(item);
            })
            .SaveAtEnd()  // F6: ذرّيّ مع أيّ interceptor يُضيف entity مرافقة
            .Build();

        var env = await _engine.ExecuteEnvelopeAsync(op, new { userId, type }, ct);
        if (env.Operation.Status != "Success")
        {
            _log.LogWarning(
                "Notifications.Create فشل: {Reason} ({Failed})",
                env.Operation.ErrorMessage, env.Operation.FailedAnalyzer);
        }
    }
}
