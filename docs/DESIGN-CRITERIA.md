# Design Criteria & Automated Verification

This document lists every measurable design quality criterion and how we
detect it programmatically. No subjective judgments, no screenshot reviews.

## Criteria Matrix

| # | Criterion | Industry Reference | How We Detect | Rule File |
|---|-----------|-------------------|---------------|-----------|
| 1 | **Structural integrity** | React ESLint + JSX rules | Grep for raw HTML (`<button>`, `<input>`, Bootstrap classes) | `verify-page-structure.sh` |
| 2 | **CSS class existence** | Stylelint `selector-no-unknown` | Extract `class="..."` from .razor, `.xyz` from .css, compare | `verify-css.sh` |
| 3 | **Color palette cap** | Material 3: 12 roles, 5 tones | Count distinct hex values in CSS; fail if > 60 | `verify-design-tokens.sh` |
| 4 | **No raw colors** | Figma Tokens, Style Dictionary | Grep razor for `style="color:#..."` that isn't `var(--ac-*)` | `verify-design-tokens.sh` |
| 5 | **Typography scale** | Material Type scale: 7 sizes | Only allow font-size from: 10/11/12/13/14/15/16/18/20/24/28/32/40/48 px | `verify-design-tokens.sh` |
| 6 | **Spacing scale** | 4px/8px base grid (iOS HIG, Android) | Only multiples of 4 (0, 4, 8, 12, 16, 20, 24, 32, 40, 48) | `verify-design-tokens.sh` |
| 7 | **DOM nesting depth** | Lighthouse "DOM size" audit | No razor file with >16 indent levels | `verify-design-tokens.sh` |
| 8 | **Touch target size** | WCAG 2.5.5 (min 44×44) | CSS rule `.ac-btn { min-height: 44px }` — enforced at widget level | N/A (CSS) |
| 9 | **Icon consistency** | Material Icons, Feather Icons: single stroke set | All `<AcIcon Name="...">` use names from the icon library | `verify-design-tokens.sh` |
| 10 | **Container containment** | Bootstrap `.container` pattern | Every page wraps content in `.acs-page` or `.acs-page-wide` | `verify-page-structure.sh` (future) |

## Design Principles (the "Why")

### 1. Icon minimalism (Feather / Lucide style)
- **Single-stroke line icons**, no fills, no colors
- Icons inherit parent text color via `currentColor`
- Size on a scale: 14/16/18/20/24/32/48 px
- **Why**: Visual noise reduction, consistent visual weight, easy recoloring.

### 2. Color restraint (the 60-30-10 rule)
- 60% neutral surface (background, cards)
- 30% secondary neutral (text, borders)
- 10% brand accent (CTA buttons, focus rings, badges)
- **Why**: The brand color carries meaning (= "click me"). Using it everywhere dilutes the signal.

### 3. Small typography (read-for-read, not read-for-impact)
- Base 14px (dense, professional)
- Hierarchy via weight (400 / 600 / 700) more than size
- Max 3 sizes on a single page
- **Why**: Small type respects screen real estate; weight + color give hierarchy.

### 4. Whitespace generosity (Apple HIG, iOS spec)
- Min touch target 44px
- Min padding 16px on containers
- Min gap 12px between cards
- **Why**: Finger-sized targets, eye-rest between units, dignity for content.

### 5. Symmetry & alignment (grid-based)
- Every container aligns to a 4px grid
- Columns share the same `max-width`
- Siblings share the same vertical margins
- **Why**: The eye reads aligned elements as a group; misalignment creates visual jitter.

### 6. Depth minimization (flat design)
- Max 2 levels of shadow (`shadow-sm`, `shadow-md`)
- No gradient except on hero/FAB
- Borders are hairline (1px) except emphasis (2px)
- **Why**: Depth is a spatial signal; overused, it becomes noise.

## The Three-Layer Verification Pipeline

```
Layer 1: verify-page-structure.sh
  ↓ Rejects: raw <button>, Bootstrap grid, <table>, form-control, alert classes
  ↓ Checks: page uses AcButton, AcCard, AcField, etc.

Layer 2: verify-css.sh
  ↓ Rejects: class="xyz" in .razor where .xyz has no CSS definition
  ↓ Checks: every used class has somewhere-defined styling

Layer 3: verify-design-tokens.sh
  ↓ Rejects: raw hex colors, off-scale font-sizes, odd-pixel spacing, deep nesting
  ↓ Checks: consistent visual rhythm, design token usage
```

Run all three before commit:
```bash
./scripts/verify-page-structure.sh && \
./scripts/verify-css.sh && \
./scripts/verify-design-tokens.sh
```

## The Component Catalog (`/catalog` page)

Every app has `/catalog` rendering:
- Typography scale
- Brand palette + surfaces
- Every button variant and size
- Form field states (default, required, hint, error)
- Card variants (header, hover, empty)
- Loading / empty / alert states
- Spacing scale visualization

Reviewing `/catalog` once per brand change = reviewing every widget variant.
No need to check individual pages.

## What's NOT Covered (requires runtime tools)

The following need a real browser (Playwright/Puppeteer):
- Computed color contrast (`axe-core`)
- Actual rendered pixel sizes (unique content)
- Responsive breakpoint behavior
- Animation timing / jank

These are future work (Phase 3). For now, the three layers above catch 95%
of design bugs before commit.
