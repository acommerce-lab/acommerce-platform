using ACommerce.Client.StateBridge;
using ACommerce.OperationEngine.Wire;
using Ashare.V2.Provider.Web.Store;

namespace Ashare.V2.Provider.Web.Interpreters;

public class UiInterpreter : IOperationInterpreter<AppStore>
{
    public bool CanInterpret(OperationDescriptor op) =>
        op.Type is "ui.set_theme" or "ui.set_language";

    public Task InterpretAsync(OperationDescriptor op, object? data, AppStore store, CancellationToken ct)
    {
        if (op.Type == "ui.set_theme" && op.Tags.TryGetValue("theme", out var t))
            store.Ui.Theme = t;
        if (op.Type == "ui.set_language" && op.Tags.TryGetValue("language", out var l))
            store.Ui.Language = l;
        store.NotifyChanged();
        return Task.CompletedTask;
    }
}
