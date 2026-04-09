using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Interceptors;
using ACommerce.OperationEngine.Wire;

namespace ACommerce.Client.Operations.Interceptors;

/// <summary>
/// معترض الجسر للحالة - معترض ما بعد التنفيذ يطبّق التغييرات على الـ store.
///
/// يقرأ server_envelope من ctx (الذي وضعه HttpDispatchInterceptor)،
/// ثم ينادي IStateApplier&lt;TStore&gt; ليطبّق المُفسّرات على الحالة.
///
/// هذا يجعل تكامل العميل مع طبقة الحالة معترضاً قابلاً للحقن - بدلاً من
/// أن يُجبر كل ViewModel على استدعاء StateBridge يدوياً بعد كل عملية.
/// </summary>
public class StateBridgeInterceptor : IOperationInterceptor
{
    private readonly IStateApplier _applier;

    public string Name => "StateBridgeInterceptor";
    public InterceptorPhase Phase => InterceptorPhase.Post;

    public StateBridgeInterceptor(IStateApplier applier)
    {
        _applier = applier;
    }

    public bool AppliesTo(Operation op) => op.HasTag("client_dispatch", "true");

    public async Task<AnalyzerResult> InterceptAsync(OperationContext context, OperationResult? result = null)
    {
        if (!context.TryGet<OperationEnvelope<object>>("server_envelope", out var envelope) || envelope == null)
            return AnalyzerResult.Warning("no_server_envelope");

        await _applier.ApplyAsync(envelope, context.CancellationToken);

        return new AnalyzerResult
        {
            Passed = true,
            Message = "state_applied",
            Data = new Dictionary<string, object>
            {
                ["op_type"] = envelope.Operation.Type,
                ["status"] = envelope.Operation.Status
            }
        };
    }
}

/// <summary>
/// تجريد الـ Applier - تطبيق طبقة StateBridge يُسجّل تطبيقاً لهذه الواجهة.
/// </summary>
public interface IStateApplier
{
    Task ApplyAsync(OperationEnvelope<object> envelope, CancellationToken ct = default);
}
