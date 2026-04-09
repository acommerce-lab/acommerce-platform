# Templates — the honest answer and the roadmap

> "لماذا تجنبت إنشاء مشاريع قوالب واكتفيت بمشروع الأدوات؟ كيف يخدم/يضر
> هذا بالهدف النهائي، وهو منصة إنتاج تطبيقات التجارة الإلكترونية متعددة
> البائعين في دقائق معدودة؟"

Short honest answer: I didn't skip templates — I built **one** (`ACommerce.Templates.Commerce`) with six composite components (`AcShell`, `AcProductCard`, `AcPlanCard`, `AcAuthPanel`, `AcPageHeader`, `AcChatBubble`) and then **over-rotated on the Bootstrap-compat layer** because it gave me 80% of the theming benefit with 5% of the effort. The result is that the two demo apps (Ashare + Order) both look properly branded, but each page's structure is hand-rolled from primitives rather than dropped in from a pre-assembled template. For the goal you stated — producing a production-ready multi-vendor e-commerce app in minutes — that's a gap, not an architectural choice.

This document lays out:

1. What I actually built and why.
2. What's missing and why that hurts the "app in minutes" goal.
3. The concrete template catalog we should have.
4. A priority order for filling the gap.

---

## 1. What I built and why

Three things exist today:

### `ACommerce.Widgets` — the atomic layer

`libs/frontend/ACommerce.Widgets/` with 10 components (`AcButton`, `AcCard`, `AcAlert`, `AcInput`, `AcField`, `AcBadge`, `AcSpinner`, `AcEmpty`, `AcPageHeader`, plus a handful of utilities) and a **664-line stylesheet** with two halves:

- lines 1–249 — the `:root` variables and the atomic CSS classes (`.ac-btn`, `.ac-card`, `.ac-alert`, `.ac-grid`, …).
- lines 250–664 — the **Bootstrap 5 compatibility layer** (`.btn`, `.card`, `.row`, `.col-*`, `.form-control`, `.badge`, `.list-group`, `.table`, `.progress`, `.alert`, `.spinner-border`, all the utility classes like `.text-muted`, `.d-flex`, `.mt-3`, …), every rule reading the same `--ac-*` variables.

This single file is what makes the cascade work: any Razor page — whether it was hand-written with `.btn btn-primary` class names, or composed from `<AcButton>` — gets themed once from the consuming app's `:root` override.

### `ACommerce.Templates.Commerce` — the composite layer

`libs/frontend/ACommerce.Templates.Commerce/` with six composite components:

| Template | Use |
|---|---|
| `AcShell` | Top-nav layout with brand, links, right-actions, and main content slot |
| `AcAuthPanel` | Login/register card with title, subtitle, content, and footer |
| `AcProductCard` | Product/offer card with image, meta, description, price, and action |
| `AcPlanCard` | Subscription plan card with recommended badge, features, and action |
| `AcPageHeader` | Page header with title, subtitle, and optional action buttons |
| `AcChatBubble` | Chat message bubble (incoming / outgoing variants) |

This project is **real** — Ashare.Web2 uses all six, Order.Web2 uses four. But it's also **small**. Six templates is not enough to cover a full e-commerce app.

### The Bootstrap compat layer — the pragmatic bridge

The third thing I built is not a project but an architectural decision: **pages that ship with a Blazor Web App scaffolded by default Visual Studio / `dotnet new blazor` use Bootstrap class names, so the compat layer means every such page works automatically**. When I ran the post-migration audit on Ashare.Web2, nine pages (`ListingDetails`, `CreateListing`, `NewBooking`, `Conversation`, `Messages`, `MyBookings`, `Notifications`, `MySubscription`, `BookingDetails`) were unstyled because they used raw Bootstrap classes. Adding the compat layer fixed all nine in a single file change with zero per-page edits.

That pragmatic win reinforced an intuition that turned out to be wrong: "if the compat layer is this cheap and this effective, maybe we don't need per-domain templates". We do.

---

## 2. Why this hurts the "app in minutes" goal

The goal you stated is: produce a production-ready multi-vendor e-commerce app in a few days at most, with a single AI agent doing most of the work. What slowed me down building Order.Web2 (which took the majority of the Order session) was **not** CSS. It was:

- Writing the cart page from scratch (quantity steppers, single-vendor enforcement UI, empty state, subtotal row, CTA).
- Writing the checkout page from scratch (pickup segmented control, car-details conditional fields, payment segmented control, cash-tendered with live change calculator, notes field).
- Writing the order-details page from scratch (items breakdown, pickup section, payment section, expected-change display, cancel button).
- Writing the messages list from scratch (conversation row with emoji + unread badge + last-message snippet).
- Writing the chat page from scratch (bubble wrapper with RTL-aware corner trimming + input row + send button).
- Writing the notifications page from scratch (type-coloured icons + unread dot + mark-all action).
- Writing the profile page from scratch (hero + avatar + stats + menu sections).
- Writing the settings page from scratch (theme segmented control + language segmented control + about section + sign-out).

Every one of those is a **template-shaped thing**. Each is ~100–200 lines of Razor + scoped CSS. Each would be valuable **unchanged** in the next app, because these shapes are almost identical across every e-commerce app in existence.

If `ACommerce.Templates.Commerce` had contained `AcCartPage`, `AcCheckoutPage`, `AcOrderDetailsPage`, `AcMessagesListPage`, `AcChatPage`, `AcNotificationsPage`, `AcProfilePage`, `AcSettingsPage`, Order.Web2 would have been **~300 lines total** instead of ~3,200, and the next app would be almost free.

---

## 3. The template catalog we should have

A single `ACommerce.Templates.Customer` project covering the full customer-facing flow, plus three more for merchant / admin / staff views. Each template is a **Razor component** with strongly-typed parameters, renders on top of widgets + compat layer, and stays brand-agnostic by reading only `--ac-*` variables.

### `ACommerce.Templates.Customer` — the one every app needs

| Template | Inputs |
|---|---|
| `AcHomePage` | `Categories`, `FeaturedOffers`, `Offers`, `OnSelectCategory`, `OnSelectOffer` |
| `AcCategoryRow` | `Categories`, `SelectedId`, `OnSelect` (horizontal pill scroller) |
| `AcOfferGrid` | `Offers`, `Columns`, `OnOfferClick` (responsive grid of `AcProductCard`s) |
| `AcOfferDetailsPage` | `Offer`, `InFavorites`, `OnAddToCart`, `OnToggleFavorite`, `OnChatWithStore` |
| `AcCartPage` | `Items`, `VendorName`, `OnQuantityChange`, `OnRemove`, `OnClear`, `OnCheckout` |
| `AcCheckoutPage` | `Subtotal`, `PickupType`, `PaymentMethod`, `CashTendered`, `CarModel`, `CarColor`, `CarPlate`, `Notes`, `OnChange`, `OnSubmit`, `Submitting`, `Error` |
| `AcOrderSuccessPage` | `Order` (success hero + summary) |
| `AcOrderDetailsPage` | `Order`, `OnCancel`, `CanCancel` |
| `AcOrdersListPage` | `Orders`, `OnSelectOrder`, `Filter`, `OnChangeFilter` |
| `AcFavoritesPage` | `Items`, `OnUnfavorite`, `OnSelectOffer` |
| `AcMessagesListPage` | `Conversations`, `OnSelectConversation` |
| `AcChatPage` | `Messages`, `Draft`, `OnDraftChange`, `OnSend`, `Sending`, `MySenderId` |
| `AcNotificationsPage` | `Notifications`, `OnMarkRead`, `OnMarkAllRead`, `OnSelectNotification` |
| `AcProfilePage` | `User`, `Stats`, `MenuSections`, `OnSignOut` |
| `AcSettingsPage` | `Theme`, `Language`, `OnThemeChange`, `OnLanguageChange`, `About`, `OnSignOut` |
| `AcLoginPage` | `Step`, `Phone`, `Code`, `Error`, `Busy`, `OnRequest`, `OnVerify`, `OnBack` |
| `AcBottomNav` | `Items` (NavLink list with icon + label + optional badge) |
| `AcEmptyState` | `Icon`, `Title`, `Message`, `ActionLabel`, `ActionHref` |

That's 17 templates. Order.Web2's 13 pages become **13 lines each** — one line importing the template and passing parameters. A new customer-facing app is ~100 lines of wiring plus the brand CSS.

### `ACommerce.Templates.Merchant` — for vendor dashboards

| Template | Use |
|---|---|
| `AcMerchantDashboard` | Top-line metrics + recent orders + quick actions |
| `AcMerchantOffersPage` | Offer list with edit/delete/publish toggles |
| `AcMerchantOfferEditor` | Form for creating/editing an offer (title, description, price, stock, emoji, category) |
| `AcMerchantOrdersQueue` | Pending / accepted / ready / delivered tabs with status transitions |
| `AcMerchantOrderDetails` | Order with accept/reject/mark-ready buttons |
| `AcMerchantMessagesInbox` | Conversations list from the vendor side |
| `AcMerchantProfileEditor` | Store info, working hours, category, location |

### `ACommerce.Templates.Admin` — for the platform operator

| Template | Use |
|---|---|
| `AcAdminUsersPage` | Paged list + role filter + block/unblock |
| `AcAdminVendorsPage` | Paged list + approve/suspend |
| `AcAdminOrdersPage` | Global order list with filters |
| `AcAdminModerationQueue` | Reported content + actions |
| `AcAdminMetricsPage` | Total orders/revenue/active users/new signups |

### `ACommerce.Templates.Auth` — shared auth flows

| Template | Use |
|---|---|
| `AcLoginPage` (already listed) | Phone OTP |
| `AcRegisterPage` | First-time profile setup after OTP |
| `AcForgotPasswordPage` | Email-based reset (when email 2FA is enabled) |
| `AcTermsAcceptancePage` | EULA gate |

---

## 4. Priority order for filling the gap

Not all templates are equal. Ranked by **immediate value for the next app**:

### P0 — ship in the next session

1. `AcOfferGrid` + `AcOfferDetailsPage` — every catalog app needs these.
2. `AcCartPage` + `AcCheckoutPage` — the order flow.
3. `AcOrderSuccessPage` + `AcOrderDetailsPage` + `AcOrdersListPage` — the post-order flow.
4. `AcLoginPage` — phone OTP is universal.
5. `AcEmptyState` + `AcBottomNav` — tiny but used everywhere.

With just these eight, the next app's page count drops from 13 hand-rolled pages to 8 one-line calls + five domain-specific pages.

### P1 — ship in the session after

6. `AcMessagesListPage` + `AcChatPage` + `AcNotificationsPage` — the comms trio.
7. `AcProfilePage` + `AcSettingsPage` — user preferences.
8. `AcFavoritesPage` — small but universal.

### P2 — when you start the merchant side

9. `ACommerce.Templates.Merchant` entire project.
10. `ACommerce.Templates.Admin` entire project.

### P3 — optional polish

11. Scaffold CLI: `dotnet new ac-app MyApp` that produces a fully-wired Blazor Web App with MyApp.Web2/MyApp.Api2 ready to run. This is where "minutes" becomes literal.

---

## The critical insight

**The Bootstrap compat layer in widgets.css was a good decision. The templates gap is a real omission.** Both can be true simultaneously. The compat layer says "no page is ever unstyled"; the templates would say "no page is ever written from scratch".

Those are complementary, not alternatives. We have the first. We need the second. The eight P0 templates are the highest-leverage thing the next session could ship.
