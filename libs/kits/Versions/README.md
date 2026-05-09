# Versions Kit

Drop-in app-version gating: backend interceptor + frontend page templates.

## Pattern (mirrors Auth + Subscriptions)

| Layer        | Project                                    | Role |
|--------------|--------------------------------------------|------|
| Operations   | `ACommerce.Kits.Versions.Operations`       | `AppVersion`, `VersionStatus`, `IAppVersionGate`, `VersionGateInterceptor`, tag keys. |
| Backend      | `ACommerce.Kits.Versions.Backend`          | `VersionsController` (`GET /version/check`), `AdminVersionsController` (`/admin/versions/*`), `IVersionStore`, default `StoreBackedAppVersionGate`. |
| Templates    | `ACommerce.Kits.Versions.Templates`        | `AcAppVersionGate`, `AcVersionBlockedPage`, `AcVersionBanner`, `AcAdminVersionsPage`, `AppVersionHeadersHandler`, `VersionState`. |
| Meta         | `ACommerce.Kits.Versions`                  | References all three. |

## States

```csharp
enum VersionStatus { Latest, Active, NearSunset, Deprecated, Unsupported }
```

| State        | Backend behavior              | Frontend behavior                           |
|--------------|-------------------------------|---------------------------------------------|
| Latest       | pass                          | nothing                                     |
| Active       | pass                          | nothing (or info banner if HasNewer)        |
| NearSunset   | pass                          | warning banner ("support ends soon")        |
| Deprecated   | pass                          | strong banner ("no longer supported")       |
| Unsupported  | **interceptor fails request** | full-screen `AcVersionBlockedPage`          |

## How it gates everything without touching other code

The same way `QuotaInterceptor` enforces subscriptions and `AuthZ` middleware
enforces auth: `VersionGateInterceptor` is a **PreInterceptor** registered on
`OpEngine`, with `AppliesTo(op) = !op.Type.StartsWith("version.")`. It runs
ahead of every operation across the whole service — controllers, kits, app
endpoints — without anyone modifying anything.

The interceptor reads `X-App-Version` and `X-App-Platform` from the current
HTTP request and calls `IAppVersionGate.CheckAsync`. If `Status == Unsupported`,
it returns `AnalyzerResult.Fail("version_unsupported", …)` and OpEngine aborts
the operation before it executes.

## Backend wiring

```csharp
// Program.cs
builder.Services.AddVersionsKit<EjarVersionStore>();
// (uses StoreBackedAppVersionGate by default; pre-register IAppVersionGate
//  if you want a custom gate with caching.)
```

## Frontend wiring (Blazor server / WASM)

```csharp
// Program.cs
builder.Services.AddSingleton(new AppVersionInfo(
    Platform: "web", Version: "1.2.0"));
builder.Services.AddVersionsTemplates(httpClientName: "ejar");
builder.Services.AddHttpClient("ejar", c => c.BaseAddress = ...)
                .AddHttpMessageHandler<AppVersionHeadersHandler>();
```

```razor
@* MainLayout.razor *@
<AcAppVersionGate>
    <ChildContent>
        @Body
    </ChildContent>
</AcAppVersionGate>
```

## Admin frontend

```razor
@page "/admin/versions"
@using ACommerce.Kits.Versions.Templates
<AcAdminVersionsPage HttpClientName="admin-api" />
```
