using ACommerce.OperationEngine.Core;

namespace ACommerce.OperationEngine.Interceptors;

/// <summary>
/// السجل العام للمعترضات - يُسجَّل في DI كـ Singleton.
/// عند تنفيذ كل قيد، OpEngine يستشيره عبر IInterceptorSource لجلب المعترضات المطابقة.
///
/// يدعم آلية حظر الحقن:
///   - عبر علامة sealed على القيد (يقفز كل المعترضات)
///   - عبر علامات exclude_interceptor (يستثني مُعترضاً معيناً بالاسم)
/// </summary>
public class OperationInterceptorRegistry : IInterceptorSource
{
    private readonly List<IOperationInterceptor> _interceptors = new();

    public OperationInterceptorRegistry Register(IOperationInterceptor interceptor)
    {
        _interceptors.Add(interceptor);
        return this;
    }

    public IReadOnlyList<IOperationInterceptor> All => _interceptors;

    /// <summary>
    /// يُرجع المعترضات كـ IOperationAnalyzer (يستدعيها OpEngine عبر IInterceptorSource).
    /// المعترض يُغلَّف بـ adapter يُحوّله لمحلل قياسي.
    /// </summary>
    public IEnumerable<IOperationAnalyzer> ResolveAnalyzers(Operation op, string phase)
    {
        var phaseFilter = phase == "pre" ? InterceptorPhase.Pre : InterceptorPhase.Post;
        return ResolveFor(op, phaseFilter)
            .Select(i => (IOperationAnalyzer)new InterceptorAnalyzerAdapter(i));
    }

    /// <summary>
    /// يُرجع المعترضات المطابقة لقيد معين في مرحلة معينة.
    /// يحترم علامات الختم والاستثناء.
    /// </summary>
    public IEnumerable<IOperationInterceptor> ResolveFor(Operation op, InterceptorPhase phase)
    {
        // قيد مختوم؟ لا يقبل أي معترض
        if (op.HasTag("sealed", "true"))
            yield break;

        // استثناءات بالاسم
        var excluded = op.Tags
            .Where(t => t.Key == "exclude_interceptor")
            .Select(t => t.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var interceptor in _interceptors)
        {
            // فلترة المرحلة
            if (interceptor.Phase != phase && interceptor.Phase != InterceptorPhase.Both)
                continue;

            // فلترة الاستثناءات
            if (excluded.Contains(interceptor.Name))
                continue;

            // فلترة المطابقة
            if (!interceptor.AppliesTo(op))
                continue;

            yield return interceptor;
        }
    }
}
