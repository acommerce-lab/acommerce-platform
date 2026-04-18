using ACommerce.Client.StateBridge;
using ACommerce.OperationEngine.Wire;
using Ashare.V2.Web.Store;

namespace Ashare.V2.Web.Interpreters;

/// <summary>
/// مُفسّر تفضيلات الواجهة — عمليّات محلّية فقط.
///   ui.set_theme / ui.set_culture / ui.set_city / ui.recent_search.add / favorite.toggle
/// ملاحظة: ليس عليها client_dispatch، لذلك لا تذهب HTTP. تطبّق StateBridge فوراً.
/// </summary>
public sealed class UiInterpreter : IOperationInterpreter<AppStore>
{
    public bool CanInterpret(OperationDescriptor op) =>
        op.Type is "ui.set_theme" or "ui.set_culture" or "ui.set_city"
                or "ui.recent_search.add" or "favorite.toggle";

    public Task InterpretAsync(OperationDescriptor op, object? _, AppStore store, CancellationToken ct)
    {
        switch (op.Type)
        {
            case "ui.set_theme":
                if (op.Tags.TryGetValue("theme", out var theme)) store.SetTheme(theme);
                break;

            case "ui.set_culture":
            {
                // دمج تدريجيّ: الوسوم الموجودة فقط تُعدَّل، الباقي يبقى كما هو.
                var c = store.Ui.Culture;
                if (op.Tags.TryGetValue("language", out var lang)) c = c with { Language = lang };
                if (op.Tags.TryGetValue("timezone", out var tz))   c = c with { TimeZone = tz };
                if (op.Tags.TryGetValue("currency", out var cur))  c = c with { Currency = cur };
                store.SetCulture(c);
                break;
            }

            case "ui.set_city":
                if (op.Tags.TryGetValue("city", out var city)) store.SetCity(city);
                break;

            case "ui.recent_search.add":
                if (op.Tags.TryGetValue("query", out var q)) store.AddRecentSearch(q);
                break;

            case "favorite.toggle":
                if (op.Tags.TryGetValue("listing_id", out var id))
                {
                    if (!store.FavoriteListingIds.Add(id))
                        store.FavoriteListingIds.Remove(id);
                    store.NotifyChanged();
                }
                break;
        }
        return Task.CompletedTask;
    }
}
