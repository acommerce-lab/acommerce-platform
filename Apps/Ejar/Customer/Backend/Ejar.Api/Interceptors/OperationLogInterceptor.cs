using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Interceptors;

namespace Ejar.Api.Interceptors;

/// <summary>
/// مُعترض سجل العمليّات — يطبع ملخصاً لكل عملية بعد تنفيذها.
/// يطابق أي عملية ما لم تحمل <c>audit:skip</c>.
/// </summary>
public sealed class OperationLogInterceptor : IOperationInterceptor
{
    private readonly ILogger<OperationLogInterceptor> _log;
    public OperationLogInterceptor(ILogger<OperationLogInterceptor> log) => _log = log;

    public string Name => "ejar.audit.log";
    public InterceptorPhase Phase => InterceptorPhase.Post;

    public bool AppliesTo(Operation op) => !op.HasTag("audit", "skip");

    public Task<AnalyzerResult> InterceptAsync(OperationContext ctx, OperationResult? result = null)
    {
        var op     = ctx.Operation;
        var from   = op.Parties.FirstOrDefault()?.Identity ?? "anonymous";
        var to     = op.Parties.Skip(1).FirstOrDefault()?.Identity ?? "-";
        var status = result?.Success ?? true ? "OK" : $"FAIL({result?.FailedAnalyzer})";
        _log.LogInformation("[AUDIT] {OpType} {From} → {To} [{Status}]",
            op.Type, from, to, status);
        return Task.FromResult(AnalyzerResult.Pass());
    }
}
