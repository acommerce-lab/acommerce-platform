# Kits — atomic libraries + distribution meta-packages

Each "Kit" packages one cross-cutting concern (chat, catalog, notifications…)
as **several focused csproj files**, then ships a single meta-package that
references all of them. Borrowed from Spree (`spree_core` + `spree_api` +
`spree_backend` + `spree_frontend` → `spree`) and Microsoft.AspNetCore.App
(meta-package over ~50 libs).

## Anatomy of a Kit

```
libs/kits/ACommerce.Kits.{Concern}.Backend/      ← ASP.NET Controllers + DI port
libs/kits/ACommerce.Kits.{Concern}.Templates/    ← Razor class library (drop-in components)
libs/kits/ACommerce.Kits.{Concern}/              ← meta — references the above + atoms
```

The atoms typically already exist as separate libs:
```
libs/backend/messaging/ACommerce.{Concern}.Operations    ← interfaces + service impl
libs/frontend/ACommerce.{Concern}.Client.Blazor          ← frontend client
```

## Why split this way

- **Backend** exposes HTTP/RPC routes; controllers get discovered via
  `AddApplicationPart`. Apps register a `I{Concern}Store` port that bridges
  to their data layer.
- **Templates** are Razor only — apps drop them into their pages with one
  tag. No business logic; UI assembly only.
- **Operations** holds interfaces + the service implementation that providers
  (SignalR, Redis, …) plug into. Lowest layer.
- **Client** holds the frontend abstraction (state, http, realtime bridging).

This keeps each csproj **small enough to be replaceable, large enough to be
useful** (Sam Newman). Apps can take just what they need:
- Backend-only (microservice that delegates UI elsewhere) → reference Backend + Operations.
- Frontend-only (thin storefront against a separate chat service) → reference Client + Templates.
- Full stack → reference the meta-package.

## Existing kits

| Kit | Status |
|-----|--------|
| `ACommerce.Kits.Chat` | ✅ first kit — see this folder |
| `ACommerce.Kits.Notifications` | ⏳ extract from existing `Notification.*` libs when 3rd consumer appears |
| `ACommerce.Kits.Catalog` | ⏳ pending — extract from Ejar / Ashare V2 when patterns settle |
| `ACommerce.Kits.Bookings` | ⏳ |

## Versioning rule

All Kit packages share one version. They evolve together (monorepo monoversion);
upgrading the meta-package upgrades atoms in lockstep. Avoids the dependency
hell that hits Spree consumers who pin `spree_core` and `spree_backend` to
different minor versions.
