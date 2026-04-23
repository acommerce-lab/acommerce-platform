using ACommerce.Client.StateBridge;
using ACommerce.OperationEngine.Wire;
using Ashare.V2.Admin.Web.Store;

namespace Ashare.V2.Admin.Web.Interpreters;

public class UiInterpreter : IOperationInterpreter<AppStore>
{
    public bool CanInterpret(OperationDescriptor op) =>
        op.Type is "ui.set_theme" or "ui.set_language";

    public Task InterpretAsync(OperationDescriptor op, object? data, AppStore store, CancellationToken ct)
    {
        switch (op.Type)
        {
            case "ui.set_theme":
                var t = op.Tags.GetValueOrDefault("theme") ?? "light";
                if (t is "light" or "dark") store.Ui.Theme = t;
                break;
            case "ui.set_language":
                var l = op.Tags.GetValueOrDefault("language") ?? "ar";
                if (l is "ar" or "en") store.Ui.Language = l;
                break;
        }
        store.NotifyChanged();
        return Task.CompletedTask;
    }
}
