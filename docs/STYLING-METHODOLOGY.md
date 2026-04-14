# Styling & Page Design Methodology

This document defines the workflow for building pages, templates, and widgets
with **zero styling bugs**. It replaces the "fix-a-page-at-a-time" loop with
a systematic pipeline.

## The Architecture

```
Pages (Apps/*/Frontend/*.Web/Components/Pages/*.razor)
  ↓ compose
Templates (libs/frontend/ACommerce.Templates.*/*.razor)
  ↓ compose
Widgets (libs/frontend/ACommerce.Widgets/*.razor)
  ↓ reference classes from
CSS Files:
  - libs/frontend/ACommerce.Widgets/wwwroot/widgets.css         (base tokens + widget classes)
  - libs/frontend/ACommerce.Templates.Commerce/wwwroot/templates.css
  - libs/frontend/ACommerce.Templates.Shared/wwwroot/templates-shared.css
  - Apps/*/wwwroot/app.css                                      (brand overrides)
```

**Layer rule**: a higher layer may use classes defined in lower layers, but
never the reverse. App CSS may override anything.

## The Five Laws of Page Building

### Law 1 — Pages use only templates and widgets. No raw HTML.

❌ Forbidden:
```razor
<button class="btn btn-primary">حفظ</button>
<div class="card"><div class="card-body">...</div></div>
<input type="text" class="form-control" />
<a class="btn btn-outline-primary">تفاصيل</a>
```

✅ Correct:
```razor
<AcButton Variant="primary" Text="حفظ" />
<AcCard><ChildContent>...</ChildContent></AcCard>
<AcInput Value="@x" ValueChanged="@(v => x = v)" />
<AcButton Variant="outline-primary" Text="تفاصيل" />
```

**Exception**: native HTML is allowed for types with no widget equivalent
(`<input type="date">`, `<input type="time">`, `<input type="checkbox">`),
but MUST use the `ac-input` class.

### Law 2 — Every widget declares the CSS classes it renders.

Each widget .razor file has a header comment:
```razor
@* CSS_CLASSES: ac-btn, ac-btn-primary, ac-btn-outline-primary, ac-btn-ghost,
                ac-btn-sm, ac-btn-lg, ac-btn-block, ac-spinner *@
```

This lets the verification script know which classes each widget needs.

### Law 3 — Every class used in a widget/template MUST be defined in CSS.

Enforced by `scripts/verify-css.csx`. This script:
1. Extracts every `class="..."` literal from all .razor files
2. Extracts every `.class-name` selector from all .css files
3. Fails if any used class is undefined

Run before committing:
```bash
dotnet script scripts/verify-css.csx
```

### Law 4 — Every app has a brand CSS file that overrides `:root` variables only.

App CSS (`Apps/*/wwwroot/app.css`) SHOULD:
- Override `--ac-primary`, `--ac-secondary`, `--ac-bg`, etc.
- Override app-specific classes with `!important` only when unavoidable.

App CSS SHOULD NOT:
- Redefine core widget classes (`.ac-btn`, `.ac-card`, etc.)
- Use `!important` for base styling (breaks cascade in nested contexts).

### Law 5 — Page width is controlled by a single container class.

Every page starts with:
```razor
<div class="acs-page">        @* max-width:900px, centered, 16px padding *@
    <AcPageHeader ... />
    @* content *@
</div>
```

For dashboards/listings that need more width:
```razor
<div class="acs-page acs-page-wide">  @* max-width:1200px *@
```

Never use inline `style="max-width:..."` on the page container.

## The Pre-Design Checklist (run BEFORE writing a new page)

1. Is there a template that does this? (Check `libs/frontend/ACommerce.Templates.*`)
2. Do all the widgets I need exist? (Check `libs/frontend/ACommerce.Widgets`)
3. If a widget is missing, add it to the library FIRST, including:
   - CSS_CLASSES comment listing every class it renders
   - CSS rules in `widgets.css` for every class
4. If a template is missing, add it to the library FIRST.

## The Post-Design Checklist (run AFTER writing a new page)

1. `grep -n "class=\"btn\|class=\"card\|class=\"row\|class=\"col-" MyPage.razor` → must be empty
2. `dotnet script scripts/verify-css.csx` → must exit 0
3. `dotnet build` → must succeed
4. Visual check in browser for: padding, margin, button alignment, empty states

## The Bug Hunt Workflow (when you see a broken page)

1. **Is it a missing class?** — run `verify-css.csx`
2. **Is the widget rendering the right classes?** — read the widget .razor
3. **Does the CSS have padding/margin?** — grep the class in all .css files
4. **Is there a conflicting `!important`?** — grep `!important` in app.css
5. **Is the parent container constraining width?** — inspect in browser devtools

## The Data Seeding Strategy

See `docs/SEEDING.md` for the complete seeding contract. Key rules:

1. **Demo user IDs are constants** shared across all APIs that need them:
   ```csharp
   public static readonly Guid VendorAhmed = Guid.Parse("00000000-0000-0000-0002-000000000001");
   ```
2. **Seeders check AND correct** existing data — not just check if empty.
3. **Auth controllers** find users by phone; if phone exists with wrong ID,
   the seeder deletes+recreates with correct ID.
4. **For the Order domain**, a shared database (order-shared.db) avoids ID
   mismatches entirely. Order.Api, Vendor.Api, and Order.Admin.Api all point
   to the same SQLite file.
