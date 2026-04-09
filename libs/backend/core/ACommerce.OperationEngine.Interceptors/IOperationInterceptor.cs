using ACommerce.OperationEngine.Core;

namespace ACommerce.OperationEngine.Interceptors;

/// <summary>
/// المرحلة التي يعمل فيها المعترض - قبل التنفيذ، بعده، أو كلاهما.
/// </summary>
public enum InterceptorPhase
{
    Pre,
    Post,
    Both
}

/// <summary>
/// معترض عمليات - محلل يُسجَّل عالمياً في DI ويُحقن تلقائياً في أي قيد يطابق predicate.
///
/// الفرق الوحيد بينه وبين IOperationAnalyzer العادي هو آلية الربط:
///   - IOperationAnalyzer يُربط بقيد واحد عبر .Analyze() / .PostAnalyze()
///   - IOperationInterceptor يُسجَّل مرة واحدة في DI ويعمل على كل قيد مطابق
///
/// يستفيد منه: الاشتراكات، الصلاحيات، الترجمات، التدقيق، الـ rate limiting،
/// حفظ الكيانات في قاعدة البيانات، الـ caching - كلها cross-cutting concerns.
/// </summary>
public interface IOperationInterceptor
{
    string Name { get; }
    InterceptorPhase Phase { get; }

    /// <summary>
    /// أسلوب المطابقة المرن - دالة تأخذ القيد وتُقرّر هل ينطبق المعترض أم لا.
    /// يستطيع المطوّر مطابقة العلامات، النوع، الأطراف، أو أي تركيبة منها.
    /// </summary>
    bool AppliesTo(Operation op);

    /// <summary>
    /// الوظيفة الفعلية. تأخذ نفس الـ context الذي تأخذه المحللات،
    /// وتُرجع AnalyzerResult (نفس تجريد المحللات لتوحيد المعالجة في OpEngine).
    /// </summary>
    Task<AnalyzerResult> InterceptAsync(OperationContext context, OperationResult? result = null);
}
