using ACommerce.OperationEngine.Wire;

namespace ACommerce.Client.StateBridge;

/// <summary>
/// مُفسّر عام قائم على lambda - يلفّ دالة في واجهة IOperationInterpreter.
/// مفيد عند تعريف قواعد تفسير بسيطة دون إنشاء صنف لكل عملية.
///
/// مثال:
///   registry.Add(new LambdaInterpreter&lt;AppStore&gt;(
///       canHandle: op =&gt; op.Type == "listing.create",
///       apply: async (op, data, store, ct) =&gt; store.Listings.Add((Listing)data!)));
/// </summary>
public class LambdaInterpreter<TStore> : IOperationInterpreter<TStore>
{
    private readonly Func<OperationDescriptor, bool> _canHandle;
    private readonly Func<OperationDescriptor, object?, TStore, CancellationToken, Task> _apply;

    public LambdaInterpreter(
        Func<OperationDescriptor, bool> canHandle,
        Func<OperationDescriptor, object?, TStore, CancellationToken, Task> apply)
    {
        _canHandle = canHandle;
        _apply = apply;
    }

    public bool CanInterpret(OperationDescriptor op) => _canHandle(op);

    public Task InterpretAsync(OperationDescriptor op, object? data, TStore store, CancellationToken ct = default)
        => _apply(op, data, store, ct);
}

/// <summary>
/// مُفسّر مبني على نوع العملية - يطابق Type مباشرة.
/// </summary>
public class TypedInterpreter<TStore> : IOperationInterpreter<TStore>
{
    private readonly string _opType;
    private readonly Func<OperationDescriptor, object?, TStore, CancellationToken, Task> _apply;

    public TypedInterpreter(string opType,
        Func<OperationDescriptor, object?, TStore, CancellationToken, Task> apply)
    {
        _opType = opType;
        _apply = apply;
    }

    public bool CanInterpret(OperationDescriptor op) => op.Type == _opType;

    public Task InterpretAsync(OperationDescriptor op, object? data, TStore store, CancellationToken ct = default)
        => _apply(op, data, store, ct);
}

/// <summary>
/// مُفسّر استهلاك الحصة - يقرأ من أي عملية تحمل analyzer اسمه QuotaConsumptionAnalyzer
/// ويستخرج subscription_id و used_count من بيانات المحلل.
///
/// مثال توضيحي لقوة النموذج: المُفسّر لا يعرف "ما هي" العملية،
/// يعرف فقط أنها قامت باستهلاك حصة - يمكنه إبلاغ الـ UI بانخفاض الرصيد.
/// </summary>
public class QuotaConsumptionInterpreter<TStore> : IOperationInterpreter<TStore>
{
    private readonly Func<TStore, Guid, int, int, Task> _onQuotaConsumed;

    public QuotaConsumptionInterpreter(Func<TStore, Guid, int, int, Task> onQuotaConsumed)
    {
        _onQuotaConsumed = onQuotaConsumed;
    }

    public bool CanInterpret(OperationDescriptor op) =>
        op.Analyzers.Any(a => a.Name == "QuotaConsumptionAnalyzer" && a.Passed);

    public Task InterpretAsync(OperationDescriptor op, object? data, TStore store, CancellationToken ct = default)
    {
        var analyzer = op.Analyzers.First(a => a.Name == "QuotaConsumptionAnalyzer");
        if (!analyzer.Data.TryGetValue("subscription_id", out var subIdObj)) return Task.CompletedTask;
        if (!Guid.TryParse(subIdObj?.ToString(), out var subId)) return Task.CompletedTask;

        int usedCount = analyzer.Data.TryGetValue("used_count", out var uc) && int.TryParse(uc?.ToString(), out var u) ? u : 0;
        int max = analyzer.Data.TryGetValue("max", out var mc) && int.TryParse(mc?.ToString(), out var m) ? m : -1;

        return _onQuotaConsumed(store, subId, usedCount, max);
    }
}
