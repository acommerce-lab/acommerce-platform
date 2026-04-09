# Building a frontend app

Step-by-step recipe for a new Blazor Web App on top of the widgets cascade,
using `Apps/Order.Web2` as the reference. By the end you will have a
themed, mobile-shaped shell with phone-OTP login, routing, protected
local storage auth persistence, and every page wearing your brand.

---

## 1. Create the project

```bash
mkdir -p Apps/MyApp.Web2/Components/Layout
mkdir -p Apps/MyApp.Web2/Components/Pages
mkdir -p Apps/MyApp.Web2/Services
mkdir -p Apps/MyApp.Web2/wwwroot/_framework
mkdir -p Apps/MyApp.Web2/wwwroot/lib/bootstrap-icons/fonts
```

Create `MyApp.Web2.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <RollForward>LatestMajor</RollForward>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>MyApp.Web2</RootNamespace>
    <AssemblyName>MyApp.Web2</AssemblyName>
  </PropertyGroup>

  <ItemGroup>
    <!-- Wire format — for OperationEnvelope<T> -->
    <ProjectReference Include="..\..\libs\backend\core\ACommerce.OperationEngine.Wire\ACommerce.OperationEngine.Wire.csproj" />

    <!-- Widgets + Commerce templates (the cascade) -->
    <ProjectReference Include="..\..\libs\frontend\ACommerce.Widgets\ACommerce.Widgets.csproj" />
    <ProjectReference Include="..\..\libs\frontend\ACommerce.Templates.Commerce\ACommerce.Templates.Commerce.csproj" />
  </ItemGroup>
</Project>
```

---

## 2. Copy these assets verbatim

These are the same on every app and are NOT worth re-deriving:

```bash
# blazor.web.js — .NET 9 doesn't ship this on the SDK image we use; we keep
# a copy in the repo and serve it as a static file
cp Apps/Order.Web2/wwwroot/_framework/blazor.web.js \
   Apps/MyApp.Web2/wwwroot/_framework/blazor.web.js

# Bootstrap Icons (sandbox proxies block the CDN)
cp -r Apps/Order.Web2/wwwroot/lib/bootstrap-icons/* \
      Apps/MyApp.Web2/wwwroot/lib/bootstrap-icons/
```

---

## 3. Services (copy and rename from Order.Web2)

All four of these are generic and you should copy them verbatim:

- `Services/AuthStateService.cs` — holds the JWT per-circuit AND persists it to `ProtectedLocalStorage` so a full page reload doesn't sign the user out.
- `Services/UiPreferences.cs` — theme (light/dark) + language (ar/en).
- `Services/CartService.cs` — in-memory cart with single-vendor enforcement (skip if your app doesn't have a cart).
- `Services/OrderApiClient.cs` — a tiny typed `HttpClient` wrapper that attaches `Bearer <token>` automatically and decodes responses as `OperationEnvelope<T>`. Rename to `MyApiClient` and change the constructor.

---

## 4. Program.cs

```csharp
using MyApp.Web2.Components;
using MyApp.Web2.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Per-circuit state
builder.Services.AddScoped<AuthStateService>();
builder.Services.AddScoped<UiPreferences>();
// builder.Services.AddScoped<CartService>();  // if you have a cart

// HTTP to the backend
var apiBase = builder.Configuration["MyApi:BaseUrl"] ?? "http://localhost:5101";
builder.Services.AddHttpClient("myapi", c =>
{
    c.BaseAddress = new Uri(apiBase);
    c.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddScoped<MyApiClient>(sp =>
{
    var f = sp.GetRequiredService<IHttpClientFactory>();
    var auth = sp.GetRequiredService<AuthStateService>();
    return new MyApiClient(f.CreateClient("myapi"), auth);
});

var app = builder.Build();
app.UseStaticFiles();
app.UseAntiforgery();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();
```

`appsettings.json`:

```json
{
  "MyApi": { "BaseUrl": "http://localhost:5101" },
  "Urls":  "http://localhost:5701"
}
```

---

## 5. Components/App.razor

This is where the cascade is wired:

```razor
@inject UiPreferences Ui

<!DOCTYPE html>
<html lang="@(Ui.IsArabic ? "ar" : "en")"
      dir="@(Ui.IsArabic ? "rtl" : "ltr")"
      data-theme="@Ui.Theme">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <base href="/" />
    <title>My App</title>

    @* 1. widgets defaults + Bootstrap-compat layer *@
    <link rel="stylesheet" href="_content/ACommerce.Widgets/widgets.css" />

    @* 2. commerce templates (AcShell, AcProductCard, AcAuthPanel, …) *@
    <link rel="stylesheet" href="_content/ACommerce.Templates.Commerce/templates.css" />

    @* 3. YOUR brand — overrides :root --ac-* *@
    <link rel="stylesheet" href="app.css" />

    @* 4. Bootstrap Icons (self-hosted) *@
    <link rel="stylesheet" href="lib/bootstrap-icons/bootstrap-icons.min.css" />

    <HeadOutlet />
</head>
<body>
    <Routes @rendermode="@RenderMode.InteractiveServer" />
    <script src="_framework/blazor.web.js"></script>
</body>
</html>
```

**The order of the CSS links is the cascade**:
1. `widgets.css` — defaults + Bootstrap-compat layer
2. `templates.css` — composite templates
3. **your** `app.css` — brand override (this wins)
4. `bootstrap-icons.min.css` — the icon font only

Your `app.css` overrides `--ac-primary`, fonts, radii, etc. in `:root`,
and everything else re-paints.

---

## 6. wwwroot/app.css — the brand

This is **the** file that differentiates your app. Copy the structure
from `Apps/Order.Web2/wwwroot/app.css` and change the values:

```css
:root {
    /* Brand */
    --ac-primary:        #YOUR-PRIMARY;
    --ac-primary-hover:  #YOUR-PRIMARY-DARKER;
    --ac-on-primary:     #ffffff;
    --ac-secondary:      #YOUR-SECONDARY;
    --ac-secondary-hover:#YOUR-SECONDARY-DARKER;

    /* Surfaces */
    --ac-bg:      #YOUR-PAGE-BG;
    --ac-bg-alt:  #YOUR-SECONDARY-SURFACE;
    --ac-surface: #ffffff;

    /* Text */
    --ac-text:       #1F1A16;
    --ac-text-muted: #8A7060;

    /* Geometry */
    --ac-radius-md: 0.85rem;
    --ac-radius-lg: 1.25rem;

    /* Typography */
    --ac-font-family: 'Your Font', 'Segoe UI', sans-serif;
}

html[data-theme="dark"] {
    --ac-bg:     #YOUR-DARK-BG;
    --ac-bg-alt: #YOUR-DARK-ALT;
    --ac-surface:#YOUR-DARK-SURFACE;
    --ac-text:       #FFF5EE;
    --ac-text-muted: #C9AC97;
}

/* Your app-specific layout (optional) */
.my-shell { ... }
.my-bottom-nav { ... }
```

Every `.ac-btn`, `.btn`, `.card`, `.alert`, `.form-control` in the
entire app now wears your brand. Same for dark theme — just write the
`html[data-theme="dark"]` overrides.

---

## 7. Routes.razor + Layout

`Routes.razor`:

```razor
<Router AppAssembly="typeof(Program).Assembly">
    <Found Context="routeData">
        <RouteView RouteData="routeData" DefaultLayout="typeof(MainLayout)" />
        <FocusOnNavigate RouteData="routeData" Selector="h1" />
    </Found>
    <NotFound>
        <LayoutView Layout="typeof(MainLayout)">
            <div class="page-container" style="text-align:center; padding-top:80px;">
                <h2>الصفحة غير موجودة</h2>
                <a class="btn btn-primary" href="/">العودة للرئيسية</a>
            </div>
        </LayoutView>
    </NotFound>
</Router>
```

`Components/Layout/MainLayout.razor`:

```razor
@inherits LayoutComponentBase
@inject AuthStateService Auth
@inject UiPreferences Ui
@implements IDisposable

<div class="my-shell">
    <main class="my-main">
        @Body
    </main>
    <nav class="my-bottom-nav">
        <NavLink href="/" Match="NavLinkMatch.All"><i class="bi bi-house"></i></NavLink>
        <NavLink href="/search"><i class="bi bi-search"></i></NavLink>
        <NavLink href="@(Auth.IsAuthenticated ? "/profile" : "/login")">
            <i class="bi bi-person"></i>
        </NavLink>
    </nav>
</div>

@code {
    protected override void OnInitialized()
    {
        Auth.OnChanged += R;
        Ui.OnChanged   += R;
    }

    // The single place we restore the JWT after a page reload
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender) await Auth.EnsureRestoredAsync();
    }

    private void R() => InvokeAsync(StateHasChanged);

    public void Dispose()
    {
        Auth.OnChanged -= R;
        Ui.OnChanged   -= R;
    }
}
```

---

## 8. Pages — two patterns

### Pattern A — public page (no auth dependency)

```razor
@page "/"
@inject MyApiClient Api

<h1>Home</h1>

@if (Items is null)  { <p>Loading…</p> }
else if (Items.Count == 0)  { <p>Nothing yet</p> }
else {
    @foreach (var x in Items) { <div class="card">@x.Name</div> }
}

@code {
    private List<Thing>? Items;

    protected override async Task OnInitializedAsync()
    {
        var env = await Api.GetAsync<List<Thing>>("/api/things");
        Items = env.Data ?? new();
    }

    public class Thing { public Guid Id { get; set; } public string Name { get; set; } = ""; }
}
```

### Pattern B — auth-dependent page (MUST use OnAfterRenderAsync)

Why: `ProtectedLocalStorage` is a JS interop call. JS interop is
**only available after the first render**. A page that reads
`Auth.UserId` in `OnInitializedAsync` will see null on the initial
render, even if the user is logged in.

**Rule**: any page that conditionally shows data based on
`Auth.IsAuthenticated` does its data loading in
`OnAfterRenderAsync(firstRender: true)`, and shows a skeleton
until then.

```razor
@page "/my-stuff"
@inject MyApiClient Api
@inject AuthStateService Auth

@if (Loading)
{
    <div class="empty-state">Loading…</div>
}
else if (!Auth.IsAuthenticated)
{
    <div class="empty-state">
        <p>Sign in first</p>
        <a class="btn btn-primary" href="/login">Sign in</a>
    </div>
}
else if (Items.Count == 0)
{
    <div class="empty-state">You have no stuff.</div>
}
else
{
    @foreach (var x in Items) { <div class="card">@x.Name</div> }
}

@code {
    private List<Thing> Items = new();
    private bool Loading = true;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;
        await Auth.EnsureRestoredAsync();
        if (Auth.IsAuthenticated)
        {
            var env = await Api.GetAsync<List<Thing>>(
                $"/api/my-stuff/by-user/{Auth.UserId}");
            Items = env.Data ?? new();
        }
        Loading = false;
        StateHasChanged();
    }

    public class Thing { public Guid Id { get; set; } public string Name { get; set; } = ""; }
}
```

`Apps/Order.Web2/Components/Pages/` has six live examples of this
pattern — Favorites, Messages, MyOrders, Notifications, OrderDetails,
Conversation.

---

## 9. Login with phone OTP

Copy `Apps/Order.Web2/Components/Pages/Login.razor`. It is a
two-step component (phone → OTP) that posts to your backend's
`/api/auth/sms/request` and `/api/auth/sms/verify`. The only thing
to customise is the hero background and the copy.

It calls `Auth.SignInAsync(userId, phone, name, token)` on success,
which writes the JWT to `ProtectedLocalStorage`. Next full reload,
`Auth.EnsureRestoredAsync()` reads it back.

---

## 10. Run it

```bash
# Start the backend (in another terminal)
cd Apps/MyApp.Api2 && dotnet run

# Start the frontend
cd Apps/MyApp.Web2 && ASPNETCORE_ENVIRONMENT=Development \
    dotnet run --urls http://localhost:5701
```

Open http://localhost:5701 — see your brand in full colour.

---

## 11. Common pitfalls

- **Blank pages in dark mode** → you forgot to add dark overrides for `--ac-bg` and `--ac-surface` in `html[data-theme="dark"]`. The cascade's widgets.css has reasonable defaults but you should override them with your brand's dark palette.
- **Interactive components don't respond** → you forgot `@rendermode="@RenderMode.InteractiveServer"` on `<Routes>`, or you forgot `app.UseStaticFiles()` / `app.UseAntiforgery()`, or `blazor.web.js` 404s.
- **User looks signed out after refresh** → the page is reading `Auth.IsAuthenticated` in `OnInitializedAsync`. Move the data load to `OnAfterRenderAsync(firstRender: true)` per Pattern B.
- **Bootstrap Icons show as squares** → `bootstrap-icons.min.css` loaded but the woff2 file is 404. Make sure you copied `wwwroot/lib/bootstrap-icons/fonts/`.
- **Arabic text is LTR** → `<html dir="rtl">` should be set via `UiPreferences`. Check that `Ui.IsArabic` is true.
- **`page.goto()` in Playwright kills the session** → this is *expected* — Playwright's `goto` does a full reload. Use link clicks for navigation in screenshot tests, or ensure every page has `EnsureRestoredAsync()` in `OnAfterRenderAsync`.

For the canonical reference implementation of every pattern in this
guide, read `Apps/Order.Web2/Components/` — it's the smallest complete
example.
