namespace ACommerce.Kits.Subscriptions.Backend;

/// <summary>
/// عقد تخزين الاشتراكات. التطبيق ينفّذه. الـ <see cref="SubscriptionsController"/>
/// يستهلكه — لا يَعرف شكل DB ولا EF.
///
/// <para>OpenAccess (في <see cref="SubscriptionsKitOptions"/>) يُختبَر في
/// الـ controller قبل استدعاء هذا الـ store، فإن كان مفعَّلاً يُرجع اشتراكاً
/// اصطناعيّاً ولا يلمس DB.</para>
/// </summary>
public interface ISubscriptionStore
{
    /// <summary>الاشتراك النشط لهذا المستخدم، أو null لو ما عنده.</summary>
    Task<SubscriptionView?> GetActiveAsync(string userId, CancellationToken ct);

    /// <summary>تفعيل باقة جديدة. الـ controller يستدعيها داخل OAM envelope.</summary>
    Task<SubscriptionView> ActivateAsync(string userId, string planId, CancellationToken ct);
}

public interface IPlanStore
{
    Task<IReadOnlyList<PlanView>> ListAsync(CancellationToken ct);
    Task<PlanView?> GetAsync(string planId, CancellationToken ct);
}

public interface IInvoiceStore
{
    Task<IReadOnlyList<InvoiceView>> ListForUserAsync(string userId, CancellationToken ct);
}
