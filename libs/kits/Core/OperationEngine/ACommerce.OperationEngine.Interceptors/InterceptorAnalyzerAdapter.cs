using ACommerce.OperationEngine.Core;

namespace ACommerce.OperationEngine.Interceptors;

/// <summary>
/// مُحوِّل بين IOperationInterceptor و IOperationAnalyzer.
/// السجل يُرجع المعترضات بهذه الصيغة ليستهلكها OpEngine بدون تعديل.
///
/// لاحظ أننا نمرر null للـ result لأن OpEngine يستدعي AnalyzeAsync(ctx) فقط.
/// المعترضات التي تحتاج OperationResult يجب أن تقرأها من ctx.Operation أو من حقل آخر.
/// </summary>
internal class InterceptorAnalyzerAdapter : IOperationAnalyzer
{
    private readonly IOperationInterceptor _interceptor;

    public InterceptorAnalyzerAdapter(IOperationInterceptor interceptor)
    {
        _interceptor = interceptor;
    }

    public string Name => _interceptor.Name;

    /// <summary>
    /// نُرجع قائمة فارغة - المطابقة تتم في OperationInterceptorRegistry قبل الوصول هنا،
    /// فلا حاجة لإعادة الفحص في OpEngine.ShouldRun.
    /// </summary>
    public IReadOnlyList<string> WatchedTagKeys => Array.Empty<string>();

    public Task<AnalyzerResult> AnalyzeAsync(OperationContext context)
        => _interceptor.InterceptAsync(context, null);
}
