using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Interceptors;

namespace Ashare.V2.Api.Interceptors;

/// <summary>
/// Post-phase audit interceptor — يطبع سجلاًّ مختصراً عن كلّ عمليّة بعد تنفيذها
/// (بدون قاعدة بيانات). يستبدل لاحقاً بـ JournalInterceptor الرسميّ
/// الذي يكتب إلى journal_entries عبر IRepositoryFactory.
///
/// يطابق أيّ عمليّة إلّا تلك التي تحمل <c>audit:skip</c>.
/// </summary>
public sealed class OperationLogInterceptor : IOperationInterceptor
{
    private readonly ILogger<OperationLogInterceptor> _log;
    public OperationLogInterceptor(ILogger<OperationLogInterceptor> log) => _log = log;

    public string Name => "ashare.audit.log";
    public InterceptorPhase Phase => InterceptorPhase.Post;

    public bool AppliesTo(Operation op) => !op.HasTag("audit", "skip");

    public Task<AnalyzerResult> InterceptAsync(OperationContext ctx, OperationResult? result = null)
    {
        var op  = ctx.Operation;
        var who = op.Parties.FirstOrDefault()?.Identity ?? "anonymous";
        var to  = op.Parties.Skip(1).FirstOrDefault()?.Identity ?? "-";
        var status = result?.Success ?? true ? "OK" : $"FAIL({result?.FailedAnalyzer})";
        _log.LogInformation("[AUDIT] {OpType} {From} → {To} [{Status}]",
            op.Type, who, to, status);
        return Task.FromResult(AnalyzerResult.Pass());
    }
}
