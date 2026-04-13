using ACommerce.Client.StateBridge;
using ACommerce.OperationEngine.Wire;
using Ashare.Admin.Web.Store;

namespace Ashare.Admin.Web.Interpreters;

public class UiInterpreter : IOperationInterpreter<AppStore>
{
    public bool CanInterpret(OperationDescriptor op) =>
        op.Type is "ui.set_theme" or "ui.set_language";

    public Task InterpretAsync(OperationDescriptor op, object? data, AppStore store, CancellationToken ct)
    {
        switch (op.Type)
        {
            case "ui.set_theme":
                var theme = op.Tags.GetValueOrDefault("theme") ?? "light";
                if (theme is "light" or "dark") store.Ui.Theme = theme;
                break;

            case "ui.set_language":
                var lang = op.Tags.GetValueOrDefault("language") ?? "ar";
                if (lang is "ar" or "en") store.Ui.Language = lang;
                break;
        }

        store.NotifyChanged();
        return Task.CompletedTask;
    }
}
