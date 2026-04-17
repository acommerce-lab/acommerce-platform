# Two Templates, One URL — Viewport Switch

Instead of writing one page that shape-shifts via dozens of media queries,
write **two independent template trees** and let CSS media queries hide the
one that doesn't match the current viewport.

## The widget

`AcViewportSwitch` (in `ACommerce.Widgets`) takes two `RenderFragment`
slots:

```razor
<AcViewportSwitch>
    <Desktop>
        <!-- full-featured layout: wide hero, 2-column grid, sidebars -->
        <AcCatalogHome Columns="2" HeroSubtitle="…long subtitle…" …>
            <HeroActions>
                <a href="/login"><AcButton Size="sm" Text="تسجيل الدخول" /></a>
                <AcButton Variant="ghost" Size="sm" Text="English" OnClick="…" />
            </HeroActions>
        </AcCatalogHome>
    </Desktop>

    <Mobile>
        <!-- completely different layout: single column, tight hero,
             block CTA, no English switch in hero (it's in the menu) -->
        <AcCatalogHome Columns="1" HeroSubtitle="عروض اليوم" …>
            <HeroActions>
                <a href="/login"><AcButton Size="sm" Block="true" Text="دخول" /></a>
            </HeroActions>
        </AcCatalogHome>
    </Mobile>
</AcViewportSwitch>
```

Both trees render.  `widgets.css` sets:

```css
:root { --ac-viewport-breakpoint: 768px; }

.ac-viewport-desktop { display: block; }
.ac-viewport-mobile  { display: none; }

@media (max-width: 768px) {
    .ac-viewport-desktop { display: none !important; }
    .ac-viewport-mobile  { display: block !important; }
}
```

Outcome: at 1366 px the desktop tree paints, at 390 px the mobile tree paints.
No flash, no JS round-trip, no server-side guessing.

## Why two templates instead of responsive CSS?

- The two versions often have **genuinely different information
  architecture** (nav collapses to a drawer, filters collapse to a modal,
  hero loses subtitles, CTA shifts from a side button to a full-width
  block).  Trying to express all of that with media queries makes every
  component a matrix of `@if IsMobile { ... } else { ... }` branches.

- A designer who wants a clean "mobile first" or "desktop first" layout
  can just edit one tree without worrying about breaking the other.

- Reusable template packs (AcCatalogHome, AcChatPage, etc.) already ship
  their own flex/grid responsiveness.  `AcViewportSwitch` composes them at
  the **page** level — the trees are different instantiations of the same
  templates with different parameter sets (fewer columns, tighter
  typography, collapsed actions).

## When you still need imperative knowledge

Some code (image-source selection, data-fetch budgeting) can't be
expressed with CSS alone.  Inject `ViewportState` + call `ViewportProbe.InitAsync`
once in MainLayout:

```csharp
@inject ACommerce.Culture.Blazor.ViewportState Viewport
@inject ACommerce.Culture.Blazor.ViewportProbe Probe

protected override async Task OnAfterRenderAsync(bool firstRender)
{
    if (firstRender) await Probe.InitAsync();
}

// later …
var pageSize = Viewport.IsMobile ? 10 : 30;
```

The probe uses `window.matchMedia` + a resize listener that calls back into
Blazor via `DotNetObjectReference`.

## Registration

```csharp
// Program.cs
builder.Services.AddBlazorCultureStack(); // already registers ViewportState / Probe

// App.razor
<script src="_content/ACommerce.Culture.Blazor/culture.js"></script>
```

## Lightweight alternative — per-element show/hide

If a full tree-swap is overkill for a single element, the widgets stylesheet
also exposes:

```html
<span class="ac-only-desktop">…shown ≥ 769 px…</span>
<span class="ac-only-mobile">…shown ≤ 768 px…</span>
```

## Reference implementation

`Apps/Order/Customer/Frontend/Order.Web/Components/Pages/Home.razor`
demonstrates the pattern at the catalog homepage — two instances of
`AcCatalogHome` wrapped in `AcViewportSwitch`.
