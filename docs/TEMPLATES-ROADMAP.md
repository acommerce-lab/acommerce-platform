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

This project is **real** — Ashare.Web uses all six, Order.Web uses four. But it's also **small**. Six templates is not enough to cover a full e-commerce app.

### The Bootstrap compat layer — the pragmatic bridge

The third thing I built is not a project but an architectural decision: **pages that ship with a Blazor Web App scaffolded by default Visual Studio / `dotnet new blazor` use Bootstrap class names, so the compat layer means every such page works automatically**. When I ran the post-migration audit on Ashare.Web, nine pages (`ListingDetails`, `CreateListing`, `NewBooking`, `Conversation`, `Messages`, `MyBookings`, `Notifications`, `MySubscription`, `BookingDetails`) were unstyled because they used raw Bootstrap classes. Adding the compat layer fixed all nine in a single file change with zero per-page edits.

That pragmatic win reinforced an intuition that turned out to be wrong: "if the compat layer is this cheap and this effective, maybe we don't need per-domain templates". We do.

---

## 2. Why this hurts the "app in minutes" goal

The goal you stated is: produce a production-ready multi-vendor e-commerce app in a few days at most, with a single AI agent doing most of the work. What slowed me down building Order.Web (which took the majority of the Order session) was **not** CSS. It was:

- Writing the cart page from scratch (quantity steppers, single-vendor enforcement UI, empty state, subtotal row, CTA).
- Writing the checkout page from scratch (pickup segmented control, car-details conditional fields, payment segmented control, cash-tendered with live change calculator, notes field).
- Writing the order-details page from scratch (items breakdown, pickup section, payment section, expected-change display, cancel button).
- Writing the messages list from scratch (conversation row with emoji + unread badge + last-message snippet).
- Writing the chat page from scratch (bubble wrapper with RTL-aware corner trimming + input row + send button).
- Writing the notifications page from scratch (type-coloured icons + unread dot + mark-all action).
- Writing the profile page from scratch (hero + avatar + stats + menu sections).
- Writing the settings page from scratch (theme segmented control + language segmented control + about section + sign-out).

Every one of those is a **template-shaped thing**. Each is ~100–200 lines of Razor + scoped CSS. Each would be valuable **unchanged** in the next app, because these shapes are almost identical across every e-commerce app in existence.

If `ACommerce.Templates.Commerce` had contained `AcCartPage`, `AcCheckoutPage`, `AcOrderDetailsPage`, `AcMessagesListPage`, `AcChatPage`, `AcNotificationsPage`, `AcProfilePage`, `AcSettingsPage`, Order.Web would have been **~300 lines total** instead of ~3,200, and the next app would be almost free.

---

## 3. The template catalog we should have

**A single `ACommerce.Templates.Customer` project is not enough.** Within "Customer" there are at least four fundamentally different information architectures — flat catalog (fashion / electronics), vendor-gated (restaurants / services), listings (real estate / classifieds), provider booking (medical / legal / salons). A template that assumes you have a `Cart` is meaningless for a booking app; a template that assumes you pick one vendor at a time is meaningless for a flat catalog.

The correct factoring is **two axes, not one**:

1. **Role axis** (outer boundary) — Shared / Customer / Merchant / Admin. Determines consumer, bundle size, dependency direction.
2. **Journey-shape axis** (inner split, inside Customer only) — Commerce / Vendors / Listings / Booking. Determines the information architecture.

Merchant and Admin stay as single projects because the merchant UI for a restaurant and for a clothing store share 90% of structure — differences live in parameters + `RenderFragment` slots, not in separate projects.

### Core design principles (apply to every template)

Three principles that every template in this catalog must follow:

**P-1. Flex slots everywhere.** Every template exposes `RenderFragment?` slots at the main mutable seams — above the header, below the header, before / after the main list, inside an empty state, in the page footer, inside a product card's action row. A consuming app that needs to inject a badge, a banner, an extra button, or a promo strip does so by filling a slot, **not by forking the template**. If a template doesn't have a slot where you need one, add the slot; don't re-implement the template. The minimum slot set per template is documented below, but templates should over-provide slots, not under-provide.

**P-2. DTO extension space.** Every DTO passed to a template carries an `Extra` bag (`Dictionary<string, object?>? Extra { get; init; }`) for vertical-specific or app-specific fields, plus `RenderFragment`-typed optional fields (e.g. `ExtraMeta`, `ExtraActions`) for display-time injection that the template doesn't need to understand. This is the data-side twin of the visual flex slots: the DTO stays strongly-typed for the known fields, and stays extensible for the unknown ones.

**P-3. Brand-agnostic by construction.** No hard-coded colors. No hard-coded radii, spacing, or typography. Read `var(--ac-*)` exclusively. Every brand customization happens in the consuming app's `:root` override.

### Layer S — `ACommerce.Templates.Shared` (used by every role, every vertical)

Shapes that are role-agnostic and vertical-agnostic. A merchant app needs `AcChatPage` and `AcNotificationsPage` just as much as a customer app does; an admin tool still needs `AcLoginPage`.

| Template | Key parameters | Minimum flex slots |
|---|---|---|
| `AcLoginPage` | `Step`, `Phone`, `Code`, `Error`, `Busy`, `OnRequestOtp`, `OnVerifyOtp`, `OnBack`, `BrandName`, `BrandIcon`, `BrandTagline` | `HeroExtra`, `FooterExtra`, `HintSlot` |
| `AcRegisterPage` | `Draft`, `OnFieldChange`, `OnSubmit`, `Busy`, `Error` | `AboveForm`, `BelowForm`, `ExtraFields` |
| `AcTermsAcceptancePage` | `TermsHtml`, `OnAccept`, `OnDecline`, `Busy` | `HeaderExtra`, `FooterExtra` |
| `AcShell` | `Brand`, `Links`, `RightActions`, `ChildContent`, `FooterContent` (already exists) | `TopBanner`, `BelowNav`, `AboveFooter` |
| `AcBottomNav` | `Items` (NavLink list with icon + label + optional badge) | `CenterSlot` (for fab) |
| `AcPageHeader` | `Title`, `Subtitle`, `BackHref`, `Actions` (already exists) | `AboveTitle`, `BelowTitle`, `LeadingSlot`, `TrailingSlot` |
| `AcEmptyState` | `Icon`, `Title`, `Message`, `ActionLabel`, `ActionHref` | `ExtraContent`, `BelowAction` |
| `AcErrorState` | `Title`, `Message`, `OnRetry` | `ExtraContent` |
| `AcLoadingState` | `Message` | `ExtraContent` |
| `AcChatPage` | `Messages`, `Draft`, `OnDraftChange`, `OnSend`, `Sending`, `MySenderId`, `PeerName`, `PeerAvatar` | `HeaderSlot`, `AboveInput`, `ExtraInputActions` |
| `AcMessagesListPage` | `Conversations`, `OnSelectConversation`, `Filter`, `OnFilterChange` | `HeaderSlot`, `EmptySlot`, `RowExtras` (RenderFragment<ConversationDto>) |
| `AcNotificationsPage` | `Notifications`, `OnMarkRead`, `OnMarkAllRead`, `OnSelectNotification` | `HeaderSlot`, `EmptySlot`, `RowExtras` |
| `AcProfilePage` | `User`, `Stats`, `MenuSections`, `OnSignOut` | `HeroExtra`, `AboveMenu`, `BelowMenu` |
| `AcSettingsPage` | `Theme`, `Language`, `OnThemeChange`, `OnLanguageChange`, `About`, `OnSignOut` | `ExtraSections` |

### Layer C — Customer templates, split by journey shape

**`ACommerce.Templates.Customer.Commerce`** — flat catalog → cart → checkout → order. Fashion, electronics, books, grocery-by-category, cafe deals.

| Template | Key parameters | Minimum flex slots |
|---|---|---|
| `AcCatalogHome` | `Categories`, `FeaturedOffers`, `Offers`, `OnSelectCategory`, `OnSelectOffer`, `OnSearch` | `TopBanner`, `BelowCategories`, `BeforeGrid`, `AfterGrid` |
| `AcCategoryRow` | `Categories`, `SelectedId`, `OnSelect` | `CategoryExtras` (RenderFragment<CategoryDto>) |
| `AcOfferGrid` | `Offers`, `Columns`, `OnOfferClick` | `CardExtras` (RenderFragment<OfferDto>), `EmptySlot`, `AboveGrid`, `BelowGrid` |
| `AcOfferDetailsPage` | `Offer`, `InFavorites`, `OnAddToCart`, `OnToggleFavorite`, `OnChatWithStore` | `HeroExtra`, `AboveDescription`, `BelowDescription`, `ExtraMeta`, `ExtraActions` |
| `AcCartPage` | `Items`, `VendorName`, `OnQuantityChange`, `OnRemove`, `OnClear`, `OnCheckout`, `Subtotal` | `AboveList`, `BelowList`, `RowExtras`, `AboveCheckoutCta`, `EmptySlot` |
| `AcCheckoutPage` | `Subtotal`, `Draft`, `OnFieldChange`, `OnSubmit`, `Submitting`, `Error` | `AboveForm`, `BelowForm`, `ExtraFields`, `AboveSubmit` |
| `AcOrderSuccessPage` | `Order`, `OnContinueShopping`, `OnViewOrder` | `HeroExtra`, `ExtraContent` |
| `AcOrderDetailsPage` | `Order`, `OnCancel`, `CanCancel` | `HeaderSlot`, `ExtraSections`, `AboveActions` |
| `AcOrdersListPage` | `Orders`, `OnSelectOrder`, `Filter`, `OnChangeFilter` | `HeaderSlot`, `EmptySlot`, `RowExtras` |
| `AcFavoritesPage` | `Items`, `OnUnfavorite`, `OnSelectOffer` | `HeaderSlot`, `EmptySlot`, `CardExtras` |

**`ACommerce.Templates.Customer.Vendors`** — vendor-first → vendor-detail → per-vendor cart. Restaurants, service marketplaces, some delivery. *(To be defined when we build the first vendor-first reference app. Do not speculate.)*

**`ACommerce.Templates.Customer.Listings`** — classifieds: listings grid + filters + map → listing detail → contact seller. Real estate, used goods, jobs. *(Defer until first reference app.)*

**`ACommerce.Templates.Customer.Booking`** — provider → availability → appointment. Medical, legal, tutors, salons. *(Defer until first reference app.)*

### Layer M — `ACommerce.Templates.Merchant` (single project)

Merchant shapes are 90% identical across verticals; differences live in `RenderFragment` slots inside the editors.

| Template | Use |
|---|---|
| `AcMerchantDashboard` | Top-line metrics + recent orders + quick actions |
| `AcMerchantOffersPage` | Offer list with edit/delete/publish toggles |
| `AcMerchantOfferEditor` | Form with title/description/price/stock + `ExtraFields` slot for vertical-specific inputs |
| `AcMerchantOrdersQueue` | Pending / accepted / ready / delivered tabs with status transitions |
| `AcMerchantOrderDetails` | Order with accept/reject/mark-ready buttons |
| `AcMerchantMessagesInbox` | Conversations list from the vendor side |
| `AcMerchantProfileEditor` | Store info, working hours, category, location |

### Layer A — `ACommerce.Templates.Admin` (single project)

| Template | Use |
|---|---|
| `AcAdminUsersPage` | Paged list + role filter + block/unblock |
| `AcAdminVendorsPage` | Paged list + approve/suspend |
| `AcAdminOrdersPage` | Global order list with filters |
| `AcAdminModerationQueue` | Reported content + actions |
| `AcAdminMetricsPage` | Total orders/revenue/active users/new signups |

### Layer V — `ACommerce.Templates.Verticals.*` (optional overlays)

Created only when a vertical-specific component has proven useful in **two or more real apps**. Not speculative. Examples of what *might* eventually live here:

- `Verticals.Restaurants` — `AcMenuModifierSheet`, `AcKitchenTicket`, `AcTableMap`
- `Verticals.RealEstate` — `AcMapClusterView`, `AcFloorPlanGallery`
- `Verticals.Healthcare` — `AcAppointmentSlotPicker`, `AcConsentForm`

### How a real app consumes this

| App | Projects it references |
|---|---|
| Clothing store | `Widgets` + `Templates.Shared` + `Templates.Customer.Commerce` |
| Cafe / deals (Order) | `Widgets` + `Templates.Shared` + `Templates.Customer.Commerce` |
| Restaurants (future) | `Widgets` + `Templates.Shared` + `Templates.Customer.Vendors` + `Verticals.Restaurants` |
| Real estate (Ashare) | `Widgets` + `Templates.Shared` + `Templates.Customer.Listings` |
| Medical booking | `Widgets` + `Templates.Shared` + `Templates.Customer.Booking` |
| Vendor dashboard (any vertical) | `Widgets` + `Templates.Shared` + `Templates.Merchant` |
| Admin console | `Widgets` + `Templates.Shared` + `Templates.Admin` |

---

## 4. Priority order for filling the gap

Ranked by **immediate value for the current reference apps** (Order + Ashare) and the next app.

### P0 — this session: everything Order and Ashare already need

Ship `Templates.Shared` and `Templates.Customer.Commerce` complete enough to replace every hand-rolled page in `Order.Web` and the Commerce-shaped pages in `Ashare.Web`. Migrate both apps to consume the new templates.

**In `Templates.Shared`:**
1. `AcLoginPage` — phone OTP flow (both apps use this shape).
2. `AcEmptyState`, `AcErrorState`, `AcLoadingState` — tiny, universal.
3. `AcBottomNav` — bottom tab bar (both apps).
4. `AcPageHeader` — extend the existing one with flex slots.
5. `AcChatPage` — both apps have chat.
6. `AcMessagesListPage` — both apps have a conversations list.
7. `AcNotificationsPage` — both apps.
8. `AcProfilePage` — both apps have a profile shape.
9. `AcSettingsPage` — both apps have a settings shape (theme + language + sign out).

**In `Templates.Customer.Commerce`:**
10. `AcCatalogHome` — Order uses this as its home.
11. `AcOfferGrid` + `AcCategoryRow` — supporting pieces for the home.
12. `AcOfferDetailsPage` — Order has this shape; Ashare's `ListingDetails` is a listings-shaped cousin and will move to `Customer.Listings` in P1.
13. `AcCartPage` — Order only (Ashare has no cart).
14. `AcCheckoutPage` — Order only.
15. `AcOrderSuccessPage` + `AcOrderDetailsPage` + `AcOrdersListPage` — Order's post-order flow.
16. `AcFavoritesPage` — Order has this shape.

At the end of P0, Order.Web pages are each ≤ 30 lines (template import + data wiring), and all Commerce-shaped Ashare pages are too. Everything is validated by visual regression screenshots before/after.

### P1 — next session

17. `AcRegisterPage`, `AcTermsAcceptancePage` — round out Shared auth.
18. Start `Templates.Customer.Listings` by extracting Ashare's `Home`, `ListingDetails`, `CreateListing`, `NewBooking`, `MyBookings`, `BookingDetails`.
19. Start `Templates.Customer.Vendors` by building a restaurant-shaped reference app.

### P2 — merchant + admin

20. `Templates.Merchant` entire project.
21. `Templates.Admin` entire project.

### P3 — optional polish

22. Scaffold CLI: `dotnet new ac-app MyApp` that produces a fully-wired Blazor Web App with `MyApp.Web` / `MyApp.Api` ready to run.

---

## The critical insight

**The Bootstrap compat layer in widgets.css was a good decision. The templates gap is a real omission.** Both can be true simultaneously. The compat layer says "no page is ever unstyled"; the templates say "no page is ever written from scratch".

Those are complementary, not alternatives. We have the first. P0 delivers the second for Commerce-shaped apps. The remaining journey shapes (Vendors, Listings, Booking) are built against real reference apps, never speculatively.
