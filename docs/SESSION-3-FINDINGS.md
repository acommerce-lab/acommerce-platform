# Session 3 — Chat Bubble, Unstyled Shells, and UI-op Routing

This round started with four user-reported defects and one architectural
concern. Each was converted into a **check** first, then a **fix**.

## User-reported defects

| # | Issue | Detection layer added | Fix applied |
|---|-------|------------------------|-------------|
| 1 | Chat-bubble text spilled past the orange fill; no padding on sides | Layer 6 **H-overflow**, widget-contracts `.act-bubble` | Added `display:inline-block`, `overflow-wrap:anywhere`, `word-break:break-word`, `box-sizing:border-box`, background fallback |
| 2 | Widgets rendered outside their parent frame | Layer 6 **L-box-model** (`box-sizing:border-box` + symmetric padding ≥ 4 px on all four sides) | Added `box-sizing: border-box` to `.ac-btn`, `.ac-card*`, `.ac-input`, `.ac-alert`, `.act-bubble`, `.s-card` |
| 3 | Ashare-Provider navbar + footer unstyled; dashboard stats scattered | Layer 2 **Rule 11 (CSS cohesion)** + Layer 6 **J-template** (strict: shell must have bg OR border) | Ashare.Provider `App.razor` was missing `templates.css` + `templates-marketplace.css`; `.s-card` was only defined in Order apps' `app.css` — moved to `templates-shared.css`; `.act-shell` now has `background: var(--ac-bg)` |
| 4 | Language toggle in 4 admin/customer apps round-tripped to HTTP | Layer 2 **Rule 10 (ui-op-routed-to-http)** | Changed `Engine.ExecuteAsync(ClientOps.SetTheme/SetLanguage)` → `Applier.ApplyLocalAsync(...)` in 4 `Settings.razor` files |
| 5 | One DB per backend per platform | — (architectural) | Unified: all Order-platform APIs → `data/order-platform.db` + `OrderPlatformDb` InMemory; all Ashare-platform APIs → `data/ashare-platform.db` + `AsharePlatformDb` InMemory |

## New check catalog (now enforced, nothing duplicated)

### Static (Layer 2)

| Rule | Forbids | Rationale |
|---|---|---|
| **10 — ui-op-routed-to-http** | `Engine.ExecuteAsync*(ClientOps.SetLanguage / SetTheme / SignOut / SetRtl)` | These are pure client-side state mutations; sending them to the backend breaks language toggling when the API is down and wastes a round-trip. |
| **11 — missing-css-link** | App references `libs/frontend/<Lib>` in `.csproj` but `App.razor` never links `_content/<Lib>/*.css` | Template widgets instantiated via project reference render unstyled (missing `AcShell`, `AcChatBubble`, etc.). |

### Runtime (Layer 6)

| Rule | Catches | Detection method |
|---|---|---|
| **H — text-overflow** | `scrollWidth > clientWidth + 2` on `.act-bubble`, `.ac-card`, `.ac-alert`, `.ac-btn`, page children, bottom nav | Text bleeding past the coloured pill / container |
| **I — cluster-atomicity** | `.s-card` / `.ac-stat-card` taller than 200 px | Lost grid/flex layout of stat clusters |
| **J — template-compliance** | Shell / navbar / footer with neither background nor border | Template CSS failed to load or was not linked |
| **K — ui-only-ops-routed-to-http** | A language/theme toggle click fires any `/api/*` request | Verifies the **fix** of static rule 10 actually works at runtime |
| **L — box-model hygiene** | Any widget computing `box-sizing: content-box`, or having `0 px` on any side of padding | Prevents `width: 100% + padding` from pushing elements outside their frame |

### Widget contracts (new selectors)

`.act-bubble`, `.s-card`, `.act-navbar`, `.act-footer` — each declares its
required properties so Layer 5 fails the build if a theme override
accidentally zeroes out a critical property.

## Final verification

```
Static layers 1–5:   0 hard violations
Runtime (anonymous): 70 violations (down from 549)
Runtime (auth):      0 violations across all 6 apps
```

Remaining anonymous violations are dominated by **contrast on auth-gated
pages we can't actually authenticate for via anonymous crawl**, and a
handful of `F-computed` drifts inside third-party font pixel rounding.
The screenshots under `docs/screenshots/session-3/` show the dramatic
recovery of Ashare-Provider's navbar/footer.

## Cross-layer cohesion, recap

Each concern lives in exactly one layer:

- **Is the operation called?** Static Rule 10.
- **Is the right CSS linked?** Static Rule 11.
- **Does the bundle render?** Runtime J (shell styled).
- **Does text fit the frame?** Runtime H (overflow) and L (box model).
- **Does a click leak to the backend?** Runtime K.
- **Do design-token counts stay healthy per app?** Layer 4 (already).
