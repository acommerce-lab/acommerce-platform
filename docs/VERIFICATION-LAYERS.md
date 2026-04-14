# The Four Verification Layers — what each catches

There are FOUR distinct layers, each with a different responsibility.
Running all four gives near-complete coverage without needing manual
screenshot review.

## Quick reference

| Script | Layer | Type | Catches | Example failure |
|--------|-------|------|---------|-----------------|
| `verify-page-structure.sh` | 1 — **Code hygiene** | Forbidden patterns | Raw `<button>`, Bootstrap classes, `<table>` | `<button class="btn">` |
| `verify-css.sh` | 2 — **Existence** | Missing definitions | Used class has no CSS | `class="foo"` but no `.foo {}` |
| `verify-design-tokens.sh` | 3 — **Direct values** | Raw values that should be tokens | Inline hex, off-scale font-size | `style="color:#ff0000"` |
| `verify-design-quality.sh` | 4 — **Visual quality** | Systemic design metrics | Too many font-sizes, broken rhythm | 45 distinct font-sizes |

## Layer-by-layer

### Layer 1 — Code Hygiene (`verify-page-structure.sh`)
**Question it answers**: "Are pages written using the component library correctly?"
**Catches**: Use of raw HTML instead of widget components.
**Example**:
```razor
✗ <button class="btn btn-primary">Save</button>
✓ <AcButton Variant="primary" Text="Save" />
```
**Failure = build breaker**.

### Layer 2 — Class Existence (`verify-css.sh`)
**Question**: "Does every class used in razor actually exist in CSS?"
**Catches**: Typos, undocumented classes, components referring to nonexistent styles.
**Example**:
```razor
<div class="acm-typo-class">  <!-- no CSS defines this -->
```
**Failure = build breaker**.

### Layer 3 — Direct Values (`verify-design-tokens.sh`)
**Question**: "Are we using design tokens, or hardcoded values?"
**Catches**: Inline hex colors, px font-sizes, odd-pixel spacing.
**Example**:
```razor
✗ style="color:#ff6600;padding:13px"
✓ style="color:var(--ac-primary);padding:var(--ac-space-md)"
```
**Failure = build breaker**.

### Layer 4 — Design Quality (`verify-design-quality.sh`)
**Question**: "Is our visual system internally consistent?"
**Catches**: Systemic issues that no single line shows — emergent properties
of the whole codebase:
- **Spacing rhythm**: how many distinct spacing values are used platform-wide?
- **Typography scale**: how many distinct font-sizes?
- **Color palette**: how many distinct colors?
- **Icon consistency**: are icons on a size scale?
- **Widget distribution**: which widgets dominate (= app is well-abstracted)?
- **Per-page sibling consistency**: does a page mix sm+lg buttons in the same view?
- **Container hierarchy**: every page starts with approved root?

**Example metrics from current codebase**:
```
Distinct spacing values: 32  ← target ≤ 20 (we use 10px, 11px, 13px, 15px, 80px, etc — no rhythm)
Distinct font-sizes:     45  ← target ≤ 8  (MASSIVE problem — no clear hierarchy)
Distinct colors:        119  ← target ≤ 60 (palette drift)
Distinct icon sizes:      8  ← target ≤ 5  (too varied)
```

**Failure = warning, not build breaker**. These metrics improve over time
as the platform matures. Hard-fail would prevent all commits.

## Why four layers, not one?

The four layers detect **increasing abstraction**:

1. **Lexical**: "the string `class=\"btn\"` appears" — regex match
2. **Semantic**: "the class named `foo` has no corresponding rule" — set comparison
3. **Stylistic**: "this inline style uses a hex value instead of a token" — value inspection
4. **Systemic**: "the platform uses 45 distinct font-sizes" — aggregate statistics

A single-layer tool would either:
- Flag too much (all inline styles are suspicious)
- Miss too much (token-compliant code can still produce inconsistent designs)

Multi-layer verification = each layer only asks ONE question, cleanly.

## The full pipeline

```bash
# Layer 1 — build breaker
./scripts/verify-page-structure.sh

# Layer 2 — build breaker
./scripts/verify-css.sh

# Layer 3 — build breaker
./scripts/verify-design-tokens.sh

# Layer 4 — warning + metrics
./scripts/verify-design-quality.sh
```

All four should run in CI. Layers 1-3 block merge. Layer 4 is a report
that trends over time — if spacing values drop from 32 → 20, we know
the team is tightening discipline.

## What this does NOT cover (still needs a browser)

- **Computed color contrast** (WCAG AA 4.5:1) — needs rendered DOM
- **Actual rendered pixel sizes** under specific fonts/zoom
- **Animation timing / jank**
- **Viewport overflow on mobile**

These require Playwright/Puppeteer. Future Phase 3 work.

## For the designer / reviewer

Instead of clicking through every page:
1. Open `/catalog` in each app → verify the design system rendering
2. Run `verify-design-quality.sh` → get a score
3. If metrics trend in wrong direction, investigate that specific metric

One URL + one script = no more screenshot reviews.
