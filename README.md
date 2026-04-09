# ACommerce Platform

Multi-vendor e-commerce platform built on the accounting OperationEngine.

**Start here:** [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)

## Quick tour

- `libs/backend/core/` — the five non-negotiable core libraries (shared kernel, EF wrapper, OperationEngine, wire format, interceptors)
- `libs/backend/auth/` — authentication + JWT + SMS / Email / Nafath 2FA + permissions
- `libs/backend/messaging/` — realtime transport + in-app and Firebase notifications
- `libs/backend/sales/` — payments (Noon provider)
- `libs/backend/marketplace/` — subscriptions with quota interceptors
- `libs/backend/files/` — file storage abstractions + Local / Aliyun OSS / Google Cloud providers
- `libs/backend/other/` — favourites, translations
- `libs/frontend/ACommerce.Widgets/` — atomic widgets + Bootstrap compatibility layer (664 lines of CSS)
- `libs/frontend/ACommerce.Templates.Commerce/` — composite templates (AcShell, AcProductCard, …)
- `clients/` — client-side accounting libraries (wire format, HTTP dispatcher, reactive bridge)
- `Apps/Ashare.Api2` + `Apps/Ashare.Web2` — property classifieds demo
- `Apps/Order.Api2` + `Apps/Order.Web2` — cafe/restaurant deals demo (in-store + curbside pickup, no online payment)

## Documentation

All docs are under `docs/`:

- **ARCHITECTURE.md** — the top-level map.
- **ACCOUNTING-PHILOSOPHY.md** — the OpEngine mental model.
- **BUILDING-A-BACKEND.md** — step-by-step recipe for a new backend.
- **BUILDING-A-FRONTEND.md** — step-by-step recipe for a new Blazor frontend.
- **AI-AGENT-ONBOARDING.md** — the brief for future AI coding agents.
- **TEMPLATES-ROADMAP.md** — what templates exist, what's missing, and why.

## Running the demos

```bash
bash Apps/Order.Web2/run-local.sh
```

Then open http://localhost:5701.
