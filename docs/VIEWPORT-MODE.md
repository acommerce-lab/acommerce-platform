# One Template, One HTML Tree, Two Appearances

The right pattern (after iterating) is **not** to write two template trees.
It is to write **one** template and let a single global variable reshape
its CSS.

## The global variable

JavaScript (`culture.js`) sets:

```
<html data-ac-mode="mobile"   ← viewport ≤ 768 px
<html data-ac-mode="desktop"  ← viewport > 768 px
```

updated on load, `matchMedia` crossings, and every resize.  This is the
single source of truth — every widget, shell, and page reads from it.

## How widgets use it

### Option A — CSS only (zero widget changes)

Write paired rules targeting the global attribute:

```css
.ord-top-nav    { display: none; /* default = mobile hides it */ }
.ord-bottom-nav { display: flex; /* default = mobile shows it */ }

:root[data-ac-mode="desktop"] .ord-top-nav    { display: block; }
:root[data-ac-mode="desktop"] .ord-bottom-nav { display: none; }
```

The MainLayout renders **both** navbars always; CSS decides which paints.
No `AcViewportSwitch`, no duplicated Razor markup.

### Option B — utility classes

Widgets can opt out of parts of their own markup:

```razor
<span class="ac-mode-mobile"><MobileOnlyControl /></span>
<span class="ac-mode-desktop"><DesktopOnlyControl /></span>
```

### Option C — C# class suffix (the "perfect" form)

Inject `IViewportMode`, emit a class modifier:

```razor
@inject IViewportMode Vp

<div class="ac-card @Vp.Modifier("ac-card")">
    @* renders "ac-card ac-card--mobile" or "ac-card ac-card--desktop" *@
</div>
```

```css
.ac-card--mobile  { padding: 8px;  border-radius: 8px; }
.ac-card--desktop { padding: 24px; border-radius: 16px;
                     box-shadow: 0 4px 20px rgba(0,0,0,0.08); }
```

Or pick between two values inline:

```razor
<AcCatalogHome Columns="@Vp.Pick(mobile: 1, desktop: 3)" … />
```

## Reference implementation

`Apps/Order/Customer/Frontend/Order.Web` demonstrates the full pattern:

- **MainLayout.razor** always renders a `.ord-top-nav` (brand + links +
  cart + sign-in) AND a `.ord-bottom-nav` (home / search / cart /
  orders / profile).  `app.css` has `:root[data-ac-mode="desktop"]`
  selectors that:
    * show `.ord-top-nav`
    * hide `.ord-bottom-nav`
    * widen `.ord-shell` max-width from the mobile-column to 1200 px
    * drop the bottom padding reserved for the (now hidden) bottom nav.

- **Home.razor** uses `@Vp.Pick(1, 3)` for the grid column count so
  the catalog lays out denser on desktop — without the page rendering
  two copies of itself.

Outcome (`docs/screenshots/session-5/viewports/mode-desktop.png` vs.
`mode-mobile.png`):

| Viewport | Top nav | Bottom nav | Grid columns | Max width |
|---|---|---|---|---|
| Desktop  1366 × 900 | visible | hidden  | 3 | 1200 px |
| Mobile    390 × 844 | hidden  | visible | 1 | 420 px (phone column) |

Same data, same colors, same Razor pages.

## What this replaces

`AcViewportSwitch` (tree-swap widget from the previous iteration) still
exists and is still useful when two viewports need **fundamentally
different information architecture** (e.g. a single-page mobile wizard
vs. a multi-pane desktop dashboard).  For 90 % of cases the `[data-ac-mode]`
pattern is cleaner.

## Registration

Nothing new — `AddBlazorCultureStack()` registers the whole culture +
viewport stack including `IViewportMode`, and `culture.js` bootstraps
`<html data-ac-mode>` on `DOMContentLoaded` without waiting for Blazor.
