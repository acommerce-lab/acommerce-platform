# Design Quality Assurance Methodology

> How to guarantee visually-consistent, high-quality page designs across
> the entire platform **without manual screenshot review**.

## The Industry Reference

We borrow from well-established approaches in React/Flutter/Angular:

| Tool | What it does | What we adopt |
|------|-------------|---------------|
| **Storybook** (React/Vue) | Isolated component catalog | Visual catalog page per app |
| **Chromatic / Percy** | Screenshot diff testing | Layout invariant checks |
| **Stylelint** | CSS lint rules | Design token enforcement |
| **axe-core** | A11y DOM checks | DOM structure checks |
| **Roslyn Analyzers** | C# AST analysis | Razor structural enforcement |

## The Three Layers of Enforcement

### Layer 1 — Structural rules (AST-based, before commit)

**Tool**: `scripts/verify-page-structure.sh`

Rules (reads .razor files, rejects violations):

| Rule | Violation | Fix |
|------|-----------|-----|
| No raw buttons | `<button class="btn">` | `<AcButton>` |
| No raw form controls | `<input class="form-control">` | `<AcInput>` (with type native for date/time/checkbox) |
| No Bootstrap grid | `<div class="row">` `<div class="col-md-6">` | CSS grid inline |
| Pages wrap in container | Missing `<div class="acs-page">` | Add container |
| No raw colors | `style="color: #ff0000"` | Use `var(--ac-*)` |

### Layer 2 — CSS class existence (scripts/verify-css.sh)

Already implemented. Every `class="..."` in .razor must exist in some .css.

### Layer 3 — Component catalog (visual identity page)

Each app has `/catalog` rendering every widget variant. One page review =
review of entire design system.

## The Guaranteed-Design Page Template

Every new page MUST follow this skeleton:

```razor
@page "/my-page"
@inject AppStore Store

<PageTitle>صفحتي - اوردر</PageTitle>

<AcAuthGuard IsAuthenticated="@Store.Auth.IsAuthenticated"
             IsArabic="@Store.Ui.IsArabic"
             LoginHref="/login">
    <div class="acs-page">
        <AcPageHeader Title="عنواني" Subtitle="وصف" />
        <AcCard>
            <ChildContent>
                @* AcField + AcInput + AcButton only *@
            </ChildContent>
        </AcCard>
        <AcButton Variant="primary" Text="حفظ" OnClick="Save" />
    </div>
</AcAuthGuard>
```

If you follow this skeleton, the page WILL look correct without visual
review. Consistency is guaranteed because:
- `.acs-page` controls max-width + padding
- Widget components control their own styling
- App CSS only overrides brand colors (`:root` variables)

## What Similar Platforms Do

### React ecosystem
- **Storybook**: `.stories.tsx` files. Run `storybook` to see the catalog.
- **Chromatic**: uploads stories, screenshots, diffs vs baseline.
- **react-styleguidist**: markdown + code examples → catalog page.

### Flutter
- **Storyboard** packages: same idea.
- **golden_tests**: PNG snapshot tests.

### Design systems at scale
- **Figma + Tokens Studio**: designers define tokens → export to CSS vars.
- **Style Dictionary**: same concept, code-side.

## Our Stack — Evolution Plan

### Phase 1 (done)
- `scripts/verify-css.sh` — CSS class existence
- `docs/STYLING-METHODOLOGY.md` — Five Laws
- `docs/SEEDING.md` — Data seeding contract

### Phase 2 (this commit)
- `scripts/verify-page-structure.sh` — Structural rules
- `/catalog` page per app — Component catalog

### Phase 3 (future, if needed)
- Roslyn analyzer: compile-time Razor rules (stronger than bash)
- Headless Playwright: real-browser render check
- Design tokens JSON: single source → generate CSS

## The Workflow

```
New page needed
    ↓
Read STYLING-METHODOLOGY.md (Five Laws)
    ↓
Copy Guaranteed-Design Template
    ↓
./scripts/verify-css.sh           (Layer 2)
./scripts/verify-page-structure.sh (Layer 1)
Browse /catalog in the app         (Layer 3)
    ↓
Commit — design is correct by construction
```

No screenshot review needed.
