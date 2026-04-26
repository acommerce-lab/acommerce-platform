using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Interceptors;
using Ashare.V2.Domain;

namespace Ashare.V2.Api.Interceptors;

/// <summary>
/// يحفظ snapshot للبيانات القابلة للتغيير بعد كل عمليّة ناجحة.
/// يضمن استمراريّة البيانات عبر إعادة تشغيل السيرفر.
/// </summary>
public sealed class PersistenceInterceptor : IOperationInterceptor
{
    public string Name => "ashare.persistence";
    public InterceptorPhase Phase => InterceptorPhase.Post;

    public bool AppliesTo(Operation op) => true; // كل Entry.Create هو mutation

    public Task<AnalyzerResult> InterceptAsync(OperationContext ctx, OperationResult? result = null)
    {
        if (result?.Success == true)
            _ = JsonSnapshotStore.SaveAsync(); // fire-and-forget
        return Task.FromResult(AnalyzerResult.Pass());
    }
}
