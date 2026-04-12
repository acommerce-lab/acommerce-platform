# Next Steps — Session 3 priorities

> Updated at the end of Session 2. This replaces the old priority list
> in TEMPLATES-ROADMAP.md. Ordered by architectural importance.

---

## P0 — Authentication as an independent service

**Problem identified in Session 2:** The Vendor.Web app calls Order.Api
for SMS OTP authentication. This is architecturally wrong — auth should
not depend on the Order domain. The user explicitly asked: "لما لا تكون
مكتبة خارجية لمزود تجريبي للرسائل او ربما خدمة خلفية كاملة مستقلة"
(why not an external library for the demo SMS provider, or a fully
independent backend service?).

**Action:**
1. Create `Apps/Auth.Api` — independent authentication service (port 5001)
   with its own database, OpEngine, and SMS/Email 2FA.
2. Both Order.Api and Vendor.Api delegate auth to Auth.Api.
3. All frontend apps point to Auth.Api for login/signup.
4. The `LoggingSmsSender` (demo) lives in Auth.Api; production swaps
   for a real SMS gateway provider.

---

## P1 — Complete Ashare.Web migration to ClientOpEngine

Ashare.Web still uses the old `AshareApiClient` + `AuthStateService`
service pattern. Migrate to `AppStore` + `ClientOpEngine` +
`OperationInterpreterRegistry<AppStore>` like Order.Web.

---

## P2 — Operation-aware Razor templates and widgets

**The user's vision:** templates and widgets should speak the accounting
model natively. Instead of:
```razor
<AcLoginPage OnRequestOtp="@RequestOtp" ... />
@code { async Task RequestOtp() { await Api.PostAsync(...); } }
```

Templates should emit operations directly:
```razor
<AcLoginPage Store="@Store" Engine="@Engine" />
```

The template internally calls `Engine.ExecuteAsync(ClientOps.RequestOtp(phone))`
— the page doesn't need a code-behind at all. The interpreter updates
the store, the template re-renders.

**This eliminates the code-behind entirely for standard flows.**

Action:
- Templates inject `ClientOpEngine` and `AppStore` directly.
- Standard operations (auth, cart, favorites) are built into templates.
- The consuming page only provides branding and label overrides.

---

## P3 — Real map integration

The current `AcMapSearchPage` uses a self-contained mini-map (CSS grid
background, no tiles). Swap the inner implementation for Leaflet.js +
OpenStreetMap tiles. The UX pattern (pins → popup → bottom sheet) stays
the same — only the map rendering changes.

---

## P4 — Production hardening

1. **Role-based auth middleware** — vendor endpoints require vendor JWT
   claims; customer endpoints require customer claims.
2. **Rate limiting** — via interceptors tagged by client type.
3. **Persistence for AppStore** — `ProtectedLocalStorage` adapter so
   auth state survives page reloads (currently lost on refresh).
4. **Real SMS provider** — Twilio/Vonage adapter for `ISmsSender`.
5. **Redis transport** — replace `InMemoryRealtimeTransport` with Redis
   for multi-instance deployments.

---

## P5 — Merchant templates library

Create `ACommerce.Templates.Merchant.Commerce` with:
- `AcVendorDashboard` — stat cards + pending orders
- `AcVendorOrderCard` — order card with action buttons by status
- `AcVendorOfferForm` — create/edit offer form
- `AcVendorSchedule` — visual work schedule editor
- `AcVendorSettings` — acceptance toggle + timeout slider

These reuse the same flex-slot pattern as customer templates.

---

## P6 — Admin dashboard

A third frontend role: platform admin. Templates for:
- Vendor approval/rejection
- Platform-wide analytics (total orders, revenue, active vendors)
- Content moderation
- Subscription plan management (Ashare)
