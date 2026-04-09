namespace Order.Web.Services;

/// <summary>
/// In-memory shopping cart. Items are kept per circuit so the user can
/// browse offers and checkout without a round-trip to the API. The cart
/// enforces the "single vendor" rule that the backend will also re-check.
/// </summary>
public class CartService
{
    public List<CartItem> Items { get; } = new();
    public Guid? VendorId { get; private set; }
    public string? VendorName { get; private set; }
    public string VendorEmoji { get; private set; } = "🏪";

    public event Action? OnChanged;

    public int Count => Items.Sum(i => i.Quantity);
    public decimal Subtotal => Items.Sum(i => i.UnitPrice * i.Quantity);

    public void Add(Guid offerId, string title, string emoji, decimal price,
                    Guid vendorId, string vendorName, string vendorEmoji)
    {
        if (VendorId.HasValue && VendorId != vendorId)
        {
            // Different vendor — start fresh
            Items.Clear();
        }
        VendorId = vendorId;
        VendorName = vendorName;
        VendorEmoji = vendorEmoji;

        var existing = Items.FirstOrDefault(i => i.OfferId == offerId);
        if (existing != null)
        {
            existing.Quantity++;
        }
        else
        {
            Items.Add(new CartItem
            {
                OfferId = offerId,
                Title = title,
                Emoji = emoji,
                UnitPrice = price,
                Quantity = 1
            });
        }
        OnChanged?.Invoke();
    }

    public void SetQuantity(Guid offerId, int qty)
    {
        var item = Items.FirstOrDefault(i => i.OfferId == offerId);
        if (item == null) return;
        if (qty <= 0) Items.Remove(item);
        else item.Quantity = qty;
        if (Items.Count == 0)
        {
            VendorId = null;
            VendorName = null;
        }
        OnChanged?.Invoke();
    }

    public void Remove(Guid offerId) => SetQuantity(offerId, 0);

    public void Clear()
    {
        Items.Clear();
        VendorId = null;
        VendorName = null;
        OnChanged?.Invoke();
    }
}

public class CartItem
{
    public Guid OfferId { get; set; }
    public string Title { get; set; } = "";
    public string Emoji { get; set; } = "🍽️";
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal LineTotal => UnitPrice * Quantity;
}
