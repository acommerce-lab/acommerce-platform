using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;

namespace ACommerce.Kits.Listings.Frontend.Customer.Stores;

/// <summary>مَصنَع عَمَليّات Listings kit. tags بِبادِئَة "qs." تَذهَب لـ query string.</summary>
public static class ListingsOps
{
    public static Operation Search(ListingFilter filter)
    {
        var b = Entry.Create("listings.search")
            .From("User:current",  1, ("role", "browser"))
            .To("Server:listings", 1, ("role", "catalog"))
            .Tag("qs.page",     filter.Page.ToString())
            .Tag("qs.pageSize", filter.PageSize.ToString());

        if (!string.IsNullOrEmpty(filter.City))         b.Tag("qs.city", filter.City);
        if (!string.IsNullOrEmpty(filter.PropertyType)) b.Tag("qs.propertyType", filter.PropertyType);
        if (!string.IsNullOrEmpty(filter.Query))        b.Tag("qs.q", filter.Query);
        if (filter.PriceMin is not null)                b.Tag("qs.priceMin", filter.PriceMin.Value.ToString());
        if (filter.PriceMax is not null)                b.Tag("qs.priceMax", filter.PriceMax.Value.ToString());
        if (filter.BedroomsMin is not null)             b.Tag("qs.bedroomsMin", filter.BedroomsMin.Value.ToString());
        return b.Build();
    }

    public static Operation GetById(string id) => Entry
        .Create("listings.get")
        .From("User:current",  1, ("role", "viewer"))
        .To($"Listing:{id}",   1, ("role", "subject"))
        .Tag("id", id)
        .Build();

    public static Operation ListMine() => Entry
        .Create("listings.list_mine")
        .From("User:current",  1, ("role", "owner"))
        .To("Server:listings", 1, ("role", "source"))
        .Build();

    /// <summary>قَيد إنشاء إعلان جَديد. السيرفر يَلتَقِطه عَبر POST /my-listings.
    /// لَو <c>IdempotencyKey</c> مُعَيَّن، يُضاف كَـ tag بِالاسم
    /// <c>idempotency_key</c> الَّذي يَفحَصه <c>IdempotencyInterceptor</c>
    /// عَلى السيرفر لِمَنع التَّكرار.</summary>
    public static Operation Create(ListingDraftPayload p)
    {
        var b = Entry.Create("listings.create")
            .From("User:current",  1, ("role", "owner"))
            .To("Server:listings", 1, ("role", "created"))
            .Tag("title",        p.Title)
            .Tag("city",         p.City)
            .Tag("propertyType", p.PropertyType ?? "");
        if (p.IdempotencyKey is { } ik)
            b.Tag("idempotency_key", ik.ToString("N"));
        return b.Build();
    }

    /// <summary>قَيد تَعديل إعلان قائِم. السيرفر يَلتَقِطه عَبر PATCH /my-listings/{id}.</summary>
    public static Operation Edit(string id, ListingDraftPayload p)
    {
        var b = Entry.Create("listings.edit")
            .From("User:current",  1, ("role", "owner"))
            .To($"Listing:{id}",   1, ("role", "edited"))
            .Tag("id",           id)
            .Tag("title",        p.Title)
            .Tag("city",         p.City)
            .Tag("propertyType", p.PropertyType ?? "");
        if (p.IdempotencyKey is { } ik)
            b.Tag("idempotency_key", ik.ToString("N"));
        return b.Build();
    }

    /// <summary>قَيد تَبديل حالة إعلان (نَشِط ↔ مُتَوَقِّف).</summary>
    public static Operation ToggleStatus(string id) => Entry
        .Create("listings.toggle")
        .From("User:current",     1, ("role", "owner"))
        .To($"Listing:{id}",      1, ("role", "toggled"))
        .Tag("id", id)
        .Build();

    /// <summary>قَيد حَذف إعلان يَملِكه المُستَخدِم.</summary>
    public static Operation Delete(string id) => Entry
        .Create("listings.delete")
        .From("User:current",     1, ("role", "owner"))
        .To($"Listing:{id}",     -1, ("role", "deleted"))
        .Tag("id", id)
        .Build();
}
