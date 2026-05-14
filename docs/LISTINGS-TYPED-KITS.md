# Listings — Typed sub-kits (deferred plan)

> Status: **deferred**. The interfaces below exist as placeholders in
> `ACommerce.Kits.Listings.Operations` so callers can begin referencing
> them, but no implementations are shipped yet. This document records
> the design intent.

## Problem

Real e-commerce / classified platforms host wildly different items in a
"listings" table: apartments, cars, halls, wedding outfits, camps. The
universal interface `IListing` only covers what's truly shared —
`Title, Price, Images, City, …`. The category-specific fields (bedrooms,
mileage, capacity, sleeves-count) cannot live on the universal
interface without bloating it and forcing all consumers to handle
"zero-valued" sentinels.

Two extremes are tempting and both wrong:

1. **JSON for everything**: cram everything into `AttributesJson`.
   Wins time-to-market, loses type safety, breaks queries, defeats the
   accounting methodology that wants typed analyzers.
2. **One entity per category**: explosion of types, lots of code for
   apps that don't need that granularity (e.g. catalogs without
   ops).

## The chosen direction

We will eventually split listings into **category-family kits**, each
with its own interface, store, controller, and widgets. The universal
`IListing` interface stays slim — just the common surface.

```
libs/kits/Listings/                          (universal — exists)
  Operations/
    IListing                                 (Title/Price/Images/City/Status/…)

libs/kits/Listings.Realty/                   (future)
  Operations/    IRealtyListing              (Bedrooms/Bathrooms/Area/Floor/Furnished/…)
  Backend/       IRealtyListingStore         (CRUD on the realty-specific shape)
  Frontend/      widgets, wizard step "realty details"

libs/kits/Listings.Vehicle/                  (future)
  Operations/    IVehicleListing             (Make/Model/Year/Mileage/FuelType/…)
  Backend/       IVehicleListingStore
  Frontend/      widgets

libs/kits/Listings.Event/                    (future)
  Operations/    IEventListing               (Capacity/Indoor/Catering/Stage/…)
  Backend/       IEventListingStore
  Frontend/      widgets

libs/kits/Listings.Apparel/                  (future)
  Operations/    IApparelListing             (Size/Color/Material/Gender/…)
  Backend/       IApparelListingStore
  Frontend/      widgets
```

### Why not build them now

- The current production app (Ejar) is a **catalog** — no category-specific
  operations (no booking-per-vehicle, no inspection-per-realty, no
  reservation-per-event). The benefits of typed kits don't materialize
  until those operations exist.
- Building 4-5 kits with empty implementations is dead code with high
  maintenance cost. Three commits later the stakeholder adds attrs and
  none of the kits help.
- The stakeholder requested immediate delivery for the next launch. Typed
  kits would block that.

### When to revisit

Build a kit when **any** of these are true for that category family:
- There's an operation that takes the typed entity as input
  (e.g. `vehicle.book` takes `IVehicleListing` and returns `IBooking`).
- There's a query that needs a strongly-typed column for performance
  (e.g. "show all vehicles with mileage < 100k" with a real index).
- There's a per-category analyzer that needs the typed fields
  (e.g. `RealtyPriceVsAreaAnalyzer` cross-validates Price vs AreaSqm).
- There's a UI that exists only for that category and warrants its own
  widget surface (vehicle card with mileage/year prominent, etc.).

### Until then

- All listings share one `ListingEntity` (per app).
- The universal interface is the slim `IListing` — see the **next
  section** for what's in it.
- Category-specific fields live in `AttributesJson` with a per-kind
  attribute template (one template per taxonomy kind: residential ⇒
  Bedrooms/Bathrooms/Area; vehicle ⇒ Make/Model/Year; …).
- Admins **do not** get a panel to manage these templates — the seed is
  in code and ships with the app version. This is intentional:
  templates ARE part of the domain shape, not configuration. Changing
  them requires a code review.

## What IListing carries now (after this PR)

Truly universal — every category in every app has these:

```csharp
public interface IListing
{
    string  Id            { get; }
    string  OwnerId       { get; }
    string  Title         { get; }
    string  Description   { get; }
    decimal Price         { get; }
    string  TimeUnit      { get; }
    string  PropertyType  { get; }   // = taxonomy slug
    string  City          { get; }
    string  District      { get; }
    double  Lat           { get; }
    double  Lng           { get; }
    int     Status        { get; }
    int     ViewsCount    { get; }
    bool    IsVerified    { get; }
    string? ThumbnailUrl  { get; }
    IReadOnlyList<string> Images { get; }
    DateTime CreatedAt    { get; }
    DateTime? UpdatedAt   { get; }
}
```

Removed from IListing (now live in `AttributesJson` per kind):
- `BedroomCount`, `BathroomCount`, `AreaSqm` — Realty-only.
- `Amenities` — Realty / Event-flavor; vehicles have "options" instead.

Apps that have existing columns for these on their entities can keep
them as legacy storage, but they're no longer part of the kit contract.
New kits should not assume their presence on the universal interface.

## Placeholder interfaces

The following live in `ACommerce.Kits.Listings.Operations` as **empty
marker interfaces** with xmldoc only. They reserve the surface for the
future sub-kits and let `IListingDetailEnricher` / the frontend signal
intent (e.g. an entity declaring `: IListing, IRealtyListing` means
"this listing has realty-specific dynamic attrs").

- `IRealtyListing`
- `IVehicleListing`
- `IEventListing`
- `IApparelListing`

They are intentionally empty for now. When a sub-kit is built, the
interface gets its members and `Listings.Operations` rev-bumps.
