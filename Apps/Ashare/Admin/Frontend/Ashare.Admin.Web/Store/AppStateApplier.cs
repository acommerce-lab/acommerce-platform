using ACommerce.Client.Operations.Interceptors;
using ACommerce.Client.StateBridge;
using ACommerce.OperationEngine.Wire;

namespace Ashare.Admin.Web.Store;

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
