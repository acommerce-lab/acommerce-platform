# ACommerce Platform

Multi-vendor e-commerce platform built on the **Operation-Accounting Model
(OAM)** — every state change is a double-entry accounting operation.

## Quick start

```bash
dotnet build ACommerce.Platform.sln
dotnet run --project Apps/Order.Api &
dotnet run --project Apps/Order.Web
# API → http://localhost:5101/swagger
# Web → http://localhost:5701
```

## Repository structure

- `libs/backend/core/` — OperationEngine, Wire, Interceptors, SharedKernel
- `libs/backend/auth/` — authentication + JWT + SMS/Email/Nafath 2FA
- `libs/backend/messaging/` — realtime transport + notifications
- `libs/backend/sales/` — payments (Noon provider)
- `libs/backend/marketplace/` — subscriptions with quota interceptors
- `libs/backend/files/` — file storage (Local / Aliyun OSS / Google Cloud)
- `libs/frontend/` — Widgets, Templates.Shared, Templates.Customer.Commerce
- `clients/` — Client.Operations, Client.Http, Client.StateBridge
- `Apps/` — Order, Vendor, Ashare (Api + Web each)

## Documentation

| Document | Contents |
|---|---|
| [`docs/MODEL.md`](docs/MODEL.md) | The Operation-Accounting Model — core concepts, lifecycle, algebraic structure |
| [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) | Four-layer architecture, library catalog |
| [`docs/LIBRARY-ANATOMY.md`](docs/LIBRARY-ANATOMY.md) | Three-layer pattern for domain libraries (pure accounting + providers + interceptors) |
| [`docs/BUILDING-A-BACKEND.md`](docs/BUILDING-A-BACKEND.md) | Step-by-step recipe for a new backend service |
| [`docs/BUILDING-A-FRONTEND.md`](docs/BUILDING-A-FRONTEND.md) | Step-by-step recipe for a new Blazor frontend |
| [`docs/ROADMAP.md`](docs/ROADMAP.md) | Current state, modification plan, session history |
| [`CLAUDE.md`](CLAUDE.md) | AI agent onboarding — laws, constraints, boundaries |
