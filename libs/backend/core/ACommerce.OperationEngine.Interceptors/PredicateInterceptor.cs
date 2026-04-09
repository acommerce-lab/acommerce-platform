using ACommerce.OperationEngine.Core;

namespace ACommerce.OperationEngine.Interceptors;

/// <summary>
/// معترض عام قائم على lambdas - مفيد للحالات السريعة دون إنشاء صنف.
///
/// مثال:
///   registry.Register(new PredicateInterceptor(
///       name: "AuditTrail",
///       phase: InterceptorPhase.Post,
///       appliesTo: op =&gt; true,  // كل العمليات
///       intercept: async (ctx, result) =&gt; {
///           await auditService.LogAsync(ctx.Operation);
///           return AnalyzerResult.Pass();
///       }));
/// </summary>
public class PredicateInterceptor : IOperationInterceptor
{
    private readonly Func<Operation, bool> _appliesTo;
    private readonly Func<OperationContext, OperationResult?, Task<AnalyzerResult>> _intercept;

    public string Name { get; }
    public InterceptorPhase Phase { get; }

    public PredicateInterceptor(
        string name,
        InterceptorPhase phase,
        Func<Operation, bool> appliesTo,
        Func<OperationContext, OperationResult?, Task<AnalyzerResult>> intercept)
    {
        Name = name;
        Phase = phase;
        _appliesTo = appliesTo;
        _intercept = intercept;
    }

    public bool AppliesTo(Operation op) => _appliesTo(op);

    public Task<AnalyzerResult> InterceptAsync(OperationContext context, OperationResult? result = null)
        => _intercept(context, result);
}

/// <summary>
/// معترض يطابق بعلامة معينة - الأكثر شيوعاً.
/// يطابق أي قيد فيه tag.key == watchedTag (وقيمته اختيارية).
/// </summary>
public class TaggedInterceptor : IOperationInterceptor
{
    private readonly string _watchedTag;
    private readonly string? _watchedValue;
    private readonly Func<OperationContext, OperationResult?, Task<AnalyzerResult>> _intercept;

    public string Name { get; }
    public InterceptorPhase Phase { get; }

    public TaggedInterceptor(
        string name,
        string watchedTag,
        InterceptorPhase phase,
        Func<OperationContext, OperationResult?, Task<AnalyzerResult>> intercept,
        string? watchedValue = null)
    {
        Name = name;
        _watchedTag = watchedTag;
        _watchedValue = watchedValue;
        Phase = phase;
        _intercept = intercept;
    }

    public bool AppliesTo(Operation op) => op.HasTag(_watchedTag, _watchedValue);

    public Task<AnalyzerResult> InterceptAsync(OperationContext context, OperationResult? result = null)
        => _intercept(context, result);
}
