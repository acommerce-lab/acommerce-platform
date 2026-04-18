using ACommerce.Client.StateBridge;
using ACommerce.OperationEngine.Wire;
using Ashare.V2.Web.Store;

namespace Ashare.V2.Web.Interpreters;

/// <summary>
/// مُفسّر تفضيلات الواجهة — عمليّات محلّية فقط.
///   ui.set_theme / ui.set_language / ui.set_city / ui.recent_search.add
/// ملاحظة: ليس عليها client_dispatch، لذلك لا تذهب HTTP. تطبّق StateBridge فوراً.
/// </summary>
public sealed class UiInterpreter : IOperationInterpreter<AppStore>
{
    public bool CanInterpret(OperationDescriptor op) =>
        op.Type is "ui.set_theme" or "ui.set_language" or "ui.set_city"
                or "ui.recent_search.add" or "favorite.toggle";

    public Task InterpretAsync(OperationDescriptor op, object? _, AppStore store, CancellationToken ct)
    {
        switch (op.Type)
        {
            case "ui.set_theme":
                if (op.Tags.TryGetValue("theme", out var theme)) store.SetTheme(theme);
                break;

            case "ui.set_language":
                if (op.Tags.TryGetValue("language", out var lang)) store.SetLanguage(lang);
                break;

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
