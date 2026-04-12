using ACommerce.Client.StateBridge;
using ACommerce.OperationEngine.Wire;
using Order.Web.Store;

namespace Order.Web.Interpreters;

/// <summary>
/// مُفسّر السلة — عمليات محلّية (لا HTTP) تحدّث CartState مباشرة.
/// </summary>
public class CartInterpreter : IOperationInterpreter<AppStore>
{
    public bool CanInterpret(OperationDescriptor op) =>
        op.Type is "cart.add" or "cart.set_quantity" or "cart.clear";

    public Task InterpretAsync(OperationDescriptor op, object? data, AppStore store, CancellationToken ct)
    {
        switch (op.Type)
        {
            case "cart.add":
            {
                var tags = op.Tags;
                var offerId = Guid.Parse(tags["offer_id"]);
                var vendorId = Guid.Parse(tags["vendor_id"]);

                // Different vendor → clear cart
                if (store.Cart.VendorId.HasValue && store.Cart.VendorId != vendorId)
                    store.Cart.Items.Clear();

                store.Cart.VendorId = vendorId;
                store.Cart.VendorName = tags.GetValueOrDefault("vendor_name");
                store.Cart.VendorEmoji = tags.GetValueOrDefault("vendor_emoji");

                var existing = store.Cart.Items.FirstOrDefault(i => i.OfferId == offerId);
                if (existing != null)
                {
                    existing.Quantity++;
                }
                else
                {
                    store.Cart.Items.Add(new CartItem
                    {
                        OfferId = offerId,
                        Title = tags.GetValueOrDefault("title") ?? "",
                        Emoji = tags.GetValueOrDefault("emoji") ?? "",
                        UnitPrice = decimal.TryParse(tags.GetValueOrDefault("price"), out var p) ? p : 0,
                        Quantity = 1
                    });
                }
                break;
            }

            case "cart.set_quantity":
            {
                var offerId = Guid.Parse(op.Tags["offer_id"]);
                var qty = int.TryParse(op.Tags.GetValueOrDefault("quantity"), out var q) ? q : 0;
                var item = store.Cart.Items.FirstOrDefault(i => i.OfferId == offerId);
                if (item != null)
                {
                    if (qty <= 0)
                        store.Cart.Items.Remove(item);
                    else
                        item.Quantity = qty;
                }
                if (store.Cart.Items.Count == 0)
                {
                    store.Cart.VendorId = null;
                    store.Cart.VendorName = null;
                }
                break;
            }

            case "cart.clear":
                store.Cart.Items.Clear();
                store.Cart.VendorId = null;
                store.Cart.VendorName = null;
                store.Cart.VendorEmoji = null;
                break;
        }

        store.NotifyChanged();
        return Task.CompletedTask;
    }
}
