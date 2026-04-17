# Culture Stack — numerals, timezones, phones, translation

A set of small, interface-driven libraries that make every culture-sensitive
operation (number parsing, timestamps, phone normalization, user-visible
text translation) live **outside** the feature code that uses it. Features
like chat, login, and orders stay untouched — the culture layer is plugged
via DI.

## Libraries

### Backend (`libs/backend/culture/`)

| Library | Contents |
|---|---|
| `ACommerce.Culture.Abstractions` | `ICultureContext`, `INumeralNormalizer`, `IDateTimeNormalizer`, `IPhoneNumberValidator` |
| `ACommerce.Culture.Defaults` | Arabic-Indic (٠-٩) + Persian (۰-۹) ↔ Latin; UTC ↔ `TimeZoneInfo`; E.164 regex phone validator; `MutableCultureContext` |
| `ACommerce.Culture.Interceptors` | EF `SaveChangesInterceptor` for numerals & UTC; ASP.NET `CultureContextMiddleware` reading `X-Timezone` / `Accept-Language` / `X-Numeral-System` |
| `ACommerce.Culture.Translation.Abstractions` | `ITranslationProvider` |
| `ACommerce.Culture.Translation.Providers.Echo` | No-op (dev / tests) |
| `ACommerce.Culture.Translation.Providers.Google` | Cloud Translate v2 |
| `ACommerce.Culture.Translation.Providers.Ai` | Anthropic / OpenAI chat-model as translator |
| `ACommerce.Culture.Phone.Providers.LibPhoneNumber` | Wrapper that will delegate to libphonenumber-csharp once the NuGet is added |

### Frontend (`libs/frontend/`)

| Library | Contents |
|---|---|
| `ACommerce.Culture.Blazor` | `BrowserCultureProbe` (reads `Intl.DateTimeFormat().resolvedOptions()`); `CultureTimeFormatter`; `wwwroot/culture.js` |

## Interceptors

| Interceptor | Layer | What it does |
|---|---|---|
| `NumeralToLatinSaveInterceptor` | EF `SavingChanges` | Walks every tracked entity's string properties and rewrites Arabic-Indic or Persian digits to Latin before they hit the DB — so `PhoneNumber="٩٦٦٥٠١١١١١١١"` stores as `+966501111111`. |
| `DateTimeUtcSaveInterceptor` | EF `SavingChanges` | Re-interprets Local/Unspecified `DateTime` fields in the current user's `ICultureContext.TimeZone` and converts them to UTC before persisting. |
| `CultureContextMiddleware` | ASP.NET | Populates the per-request `MutableCultureContext` from incoming headers. |

## Short-path wiring (no library edits required)

### Backend service (`Order.Api` example)

```csharp
using ACommerce.Culture.Interceptors;

builder.Services.AddCultureStack();    // registers ICultureContext + friends
// … rest of your builder.Services …

app.UseCultureContext();               // after UseRouting / before controllers
```

### Blazor frontend (`Order.Web` example)

```csharp
using ACommerce.Culture.Blazor;
builder.Services.AddBlazorCultureStack();
```

`App.razor`:

```html
<script src="_content/ACommerce.Culture.Blazor/culture.js"></script>
```

`MainLayout.razor`:

```razor
@inject ACommerce.Culture.Blazor.BrowserCultureProbe CultureProbe

protected override async Task OnAfterRenderAsync(bool firstRender)
{
    if (firstRender) { try { await CultureProbe.InitAsync(); } catch { } }
    // …
}
```

Any page that needs a timezone-correct timestamp:

```razor
@inject ACommerce.Culture.Blazor.CultureTimeFormatter Time

@Time.AsLocalShort(order.CreatedAt)
```

## Chat timestamps across timezones

Problem before: `AcChatPage` renders `@m.CreatedAt.ToLocalTime()`, but on
Blazor Server `ToLocalTime()` uses the **server's** TZ, not the connected
user's. Users in Riyadh and New York both saw Riyadh time.

Short-path fix **without** touching `AcChatPage`:

```csharp
private IReadOnlyList<ChatMessageDto> Mapped =>
    Messages.Select(m => new ChatMessageDto {
        …,
        CreatedAt = DateTime.SpecifyKind(
            TimeFormatter.AsLocal(m.CreatedAt),
            DateTimeKind.Local)   // mark already-converted as Local so
                                   // AcChatPage's .ToLocalTime() is idempotent
    }).ToList();
```

Each connected user now sees the timestamp in *their* browser's timezone.

## Which apps benefit

| App | Backend stack | Frontend stack | Primary value |
|---|---|---|---|
| Order.Api / Web | ✓ wired | ✓ wired | Chat timestamps, Arabic-digit phone login |
| Vendor.Api / Vendor.Web | ready | wire via `AddBlazorCultureStack()` | Same |
| Order.Admin.Api / Web | ready | wire | Admin sees timestamps in his own TZ |
| Ashare.Api / Web | ready | wire | Listings dates, chat |
| Ashare.Provider.Web | n/a (no BE) | wire | Bookings shown in provider TZ |
| Ashare.Admin.Api / Web | ready | wire | Same |

## Verified

- Phone normalization accepts every format below and still resolves to the
  single seeded vendor `00000000-0000-0000-0002-000000000001`:

  ```
  +966501111111    966501111111   0501111111   501111111
  ٠٥٠١١١١١١١       ۰۵۰۱۱۱۱۱۱۱      +٩٦٦٥٠١١١١١١١    "  0501111111 "
  ```

- `CultureTimeFormatter` formats UTC DateTimes according to the browser's
  IANA timezone, set once per Blazor circuit by `BrowserCultureProbe`.

## Not yet wired (deliberate)

- LibPhoneNumber NuGet reference — the wrapper is in place; add
  `<PackageReference Include="libphonenumber-csharp" Version="8.*" />` when
  deeper validation is needed.
- Google / AI translation providers — interfaces + HttpClient impls ready
  but not registered; supply an options object and
  `services.AddHttpClient<GoogleTranslationProvider>()` to enable.
- `DateTimeUtcSaveInterceptor` wiring to `ApplicationDbContext` — waiting
  on adding the `optionsBuilder.AddInterceptors(...)` hook inside
  `ACommerce.SharedKernel.Infrastructure.EFCores`.
