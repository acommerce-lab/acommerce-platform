using ACommerce.Client.Operations.Interceptors;
using ACommerce.Client.StateBridge;
using ACommerce.OperationEngine.Wire;

namespace Ashare.Web.Store;

/// <summary>
/// يربط StateBridgeInterceptor بـ OperationInterpreterRegistry.
/// عند نجاح عملية HTTP، المعترض ينادي ApplyAsync → نمرّر على كل المُفسّرات.
/// عند عملية محلّية (تفضيلات UI)، نستدعي ApplyLocal مباشرة.
/// </summary>
public class AppStateApplier : IStateApplier
{
    private readonly OperationInterpreterRegistry<AppStore> _registry;
    private readonly AppStore _store;

    public AppStateApplier(OperationInterpreterRegistry<AppStore> registry, AppStore store)
    {
        _registry = registry;
        _store = store;
    }

    public async Task ApplyAsync(OperationEnvelope<object> envelope, CancellationToken ct = default)
    {
        await _registry.ApplyAsync(envelope, _store, ct);
    }

    /// <summary>
    /// تطبيق عملية محلّية (لا تحتاج HTTP) — نبني مغلف مصطنع ونمرّره على المُفسّرات.
    /// </summary>
    public async Task ApplyLocalAsync(ACommerce.OperationEngine.Core.Operation op, CancellationToken ct = default)
    {
        var descriptor = OperationEnvelopeFactory.ToDescriptor(op,
            new ACommerce.OperationEngine.Core.OperationResult
            {
                OperationId = op.Id,
                OperationType = op.Type,
                Success = true
            });

        var envelope = new OperationEnvelope<object>
        {
            Operation = descriptor,
            Data = null
        };

        await _registry.ApplyAsync(envelope, _store, ct);
    }
}
