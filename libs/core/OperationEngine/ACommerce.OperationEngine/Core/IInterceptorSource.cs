namespace ACommerce.OperationEngine.Core;

/// <summary>
/// مصدر المعترضات - تجريد يُحقن في DI ليستشيره OpEngine.
/// التطبيق الفعلي موجود في مكتبة ACommerce.OperationEngine.Interceptors
/// لكن OpEngine يعرف فقط هذا التجريد لتفادي الـ circular dependency.
///
/// إذا لم يكن هناك تطبيق مُسجّل في DI، OpEngine يعمل كما كان (بدون معترضات).
/// </summary>
public interface IInterceptorSource
{
    /// <summary>
    /// يُرجع المحللات المحقونة لهذه العملية في مرحلة معينة.
    /// </summary>
    /// <param name="op">العملية</param>
    /// <param name="phase">"pre" أو "post"</param>
    IEnumerable<IOperationAnalyzer> ResolveAnalyzers(Operation op, string phase);
}
