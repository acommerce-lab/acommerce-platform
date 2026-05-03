# ACommerce Platform — Agent Onboarding

## What this is

A .NET 10 / Blazor platform for producing production-ready multi-vendor
e-commerce applications in days. The core idea: **every state change is a
double-entry accounting operation**. Read `docs/MODEL.md` before writing
any code.

## Session bootstrap

Before anything else in a fresh web session, run the two `apt-get`
commands in **`docs/DOTNET-SETUP.md`** to install .NET 10. Without it
no `.csproj` in this repo can be built.

To run Layer 6 runtime verification (Playwright), also download the
Chrome-for-Testing zip as documented in the same file — the default
Playwright CDN is blocked in sandboxed sessions.

### عشير القديم — Old Ashare reference app

**"عشير القديم"** always means the old Ashare app from the external
repository below. It does **NOT** mean Ashare V1 (`Apps/Ashare.Api` /
`Apps/Ashare.Web`) inside this repository — those are a different,
earlier implementation on this same platform.

If any task references "عشير القديم" (old Ashare) or asks you to compare
with or match the old Ashare UI/backend, clone it into `/tmp` first:

```bash
git clone https://github.com/acommerce-lab/ACommerce.Libraries /tmp/ACommerce.Libraries
```

See **`docs/DOTNET-SETUP.md`** → section "عشير القديم" for the full
loading procedure (includes the dotnet install prerequisite).

## Read order

1. **`docs/MODEL.md`** — the Operation-Accounting Model (OAM). This is the
   intellectual foundation. Without it you will write code that bypasses the
   engine and defeats the platform's purpose.
2. **`docs/ARCHITECTURE.md`** — the library map (4 layers, 40 projects).
3. **`docs/LIBRARY-ANATOMY.md`** — how domain libraries are structured
   (three-layer pattern: pure accounting + provider contracts + injected
   interceptors).
4. **`docs/BUILDING-A-BACKEND.md`** — step-by-step recipe for a new backend.
5. **`docs/BUILDING-A-FRONTEND.md`** — step-by-step recipe for a new Blazor
   frontend.
6. **`docs/I18N.md`** — the bilingual (Arabic/English) translation system
   used across all V2 frontends. Read before writing any user-facing text
   in a Razor page.
7. **`docs/PITFALLS.md`** — concrete mistakes we hit in this codebase
   and how to avoid them: provider lock-in, Singleton↔Scoped lifetime
   mismatches, SW breaking CORS, BaseAddress traps, mobile-only
   Notification API restrictions, version-bump checklist. Read before
   any task touching DI, realtime, or PWA shell.
8. **`docs/COMPOSITION-MODEL.md`** — the target architecture: pure-OAM
   kits + external compositions via interceptor bundles + strongly typed
   OAM (no string magic). Required reading before refactoring any kit
   or wiring kits together. Roadmap with phases A-E.
9. **`docs/ROADMAP.md`** — what's done, what's next, modification plan.

Reference apps (read the smallest first):
- `Apps/Order.Api` + `Apps/Order.Web` — cafe deals, cleanest example.
- `Apps/Vendor.Api` + `Apps/Vendor.Web` — vendor-side microservice.
- `Apps/Ashare.Api` + `Apps/Ashare.Web` — property classifieds, larger.

## The seven laws

### Law 1 — Every state change is an operation

Never write `_repo.AddAsync(entity)` from a controller. Every mutation:

```csharp
var op = Entry.Create("thing.create")
    .From($"User:{ownerId}", 1, ("role", "owner"))
    .To($"Thing:{id}",       1, ("role", "created"))
    .Tag("name", name)
    .Analyze(new RequiredFieldAnalyzer("name", () => name))
    .Execute(async ctx => await _repo.AddAsync(entity, ctx.CancellationToken))
    .Build();
var envelope = await _engine.ExecuteEnvelopeAsync(op, entity, ct);
```

### Law 2 — Every response is an OperationEnvelope

Read endpoints too: `return this.OkEnvelope("thing.list", data);`

### Law 3 — Repository method signatures

- `ListAllAsync(ct)` — takes CancellationToken.
- `GetAllWithPredicateAsync(predicate)` — does NOT take CancellationToken.
  Second arg is `bool includeDeleted`. Mixing them is the #1 bug.

### Law 4 — The widgets cascade is the styling system

Use `var(--ac-primary)` or Bootstrap classes. Never hard-coded colours.
Brand overrides go in the app's `wwwroot/app.css` on `:root`.

### Law 5 — Auth state survives reload via OnAfterRenderAsync

Load auth-dependent data in `OnAfterRenderAsync(firstRender: true)` after
`await Auth.EnsureRestoredAsync()`. Not in `OnInitializedAsync`.

### Law 6 — Adapt to real data shape, but enforce contracts via interfaces

Two halves; both required.

**Half 1 — adapt to data shape.** When integrating with an existing
production system, the NEW platform adapts to the shape of production data,
not the other way around. If the stakeholder's listings use `features`
instead of `amenities`, rename the template. If the production API returns a
plain array instead of OperationEnvelope, handle both. Any attribute key
not in the template is preserved as a raw `DynamicAttribute` entry. We serve
the stakeholder's existing data exactly as they expect it; the platform is a
tool, not an authority over business data.

**Half 2 — enforce contracts via interfaces, never DTOs.** Cross-cutting
libraries (chat, notifications, payments…) need to interact with the app's
domain entities — but they cannot dictate the entity's storage layout, fields,
or persistence strategy. The discipline is:

1. The library defines a **C# interface** with the minimal properties it needs
   (e.g. `IChatMessage` with `Id, ConversationId, SenderPartyId, Body, SentAt,
   ReadAt?`). Six properties — no more.
2. The app's domain entity **implements that interface** directly (often via
   explicit interface implementation when its own field names differ).
3. The library accepts and returns the interface — never a DTO. The app keeps
   its own type, with whatever extra fields it needs.

This means: *no DTO bridging in app code, no schema imposed by libraries, and
no "shadow shape" parallel to the domain entity*. Library-to-library data flow
travels as the app's own entity, viewed through the interface lens.

Reference impl: `Apps/Ejar/.../Services/EjarSeed.cs` — `MessageSeed`
implements `IChatMessage` via explicit interface members, so the chat lib
broadcasts `MessageSeed` instances directly without any conversion layer.

### Law 7 — Every user-visible string goes through `L["key"]`

Blazor apps are bilingual (Arabic/English) and the user can toggle at any
moment. Hardcoded strings freeze the page in one language and break
re-render on language change. The rule:

1. Reference implementation:
   `Apps/Order.V2/Customer/Frontend/Order.V2.Web/Store/L.cs` —
   `CustomerTranslations` extending `EmbeddedTranslationProvider` with
   `_ar` and `_en` dictionaries. Copy this pattern for new apps.
2. Register once in `Program.cs`:
   `builder.Services.AddEmbeddedL10n<AppTranslations, AppLangContext>();`
3. In pages: `@inject L L` then `@(L["home.title"])`.
4. `L.T("عربي", "english")` is a **migration-only** shortcut — extract
   to the dictionary before the PR lands.
5. `Store.Ui.IsArabic ? "…" : "…"` is a **code smell** unless it's one
   of: a language-toggle button (shows the *opposite* language),
   selecting between API `NameAr`/`NameEn` fields, or passing a bool
   to a widget's own RTL logic.

Full guide — **`docs/I18N.md`**. Read it before adding any user-facing
text.

## Constraint vocabulary — when to use what

| Tool | When | Example |
|---|---|---|
| **Analyzer** (`.Analyze()`) | Constraint local to ONE operation | `RequiredFieldAnalyzer("content")` |
| **Interceptor** (registry) | Cross-cutting, shared across operations | `QuotaInterceptor` on `tag("quota_check")` |
| **Validate** (`.Validate()`) | Needs DI/DB access, still local | Check all items from same vendor |
| **PredicateAnalyzer inline** | One-liner not worth a class | `.Analyze(new PredicateAnalyzer("x", ctx => ...))` |
| **ProviderContract** | Mandatory external dependency | `IMessageStore`, `IPaymentGateway` |
| **Sealed** (`.Sealed()`) | Block ALL interceptors | Sensitive internal operations |
| **ExcludeInterceptor** | Block ONE interceptor by name | Skip audit on health checks |

## Frontend constraint vocabulary — when to use what (client side)

The backend uses Analyzers / Interceptors / Compositions / ProviderContracts.
Frontend has its own parallel vocabulary, equally important. Apps that mix
them up grow ugly: pages doing HTTP, stores doing UI, interceptors doing
business logic.

| Tool | When | When NOT |
|---|---|---|
| **`IXxxStore`** (kit) | Reactive state shared across pages or pushed by realtime (Listings, Chat, Notifications, Auth) | Static page (About, ToS) — just render directly. Per-render-only state — use `[Parameter]` or `[CascadingParameter]`. |
| **Page-level Validator** | Form validation tied to one submit (`required`, `min-length`, `regex`) | Cross-form rule, async DB check ⇒ Client Interceptor on the dispatched op. |
| **Client Interceptor** (`IClientOperationInterceptor`) | Cross-cutting around `IClientOpEngine.DispatchAsync` (optimistic update, retry-on-401, telemetry, offline queue) | Pure UI logic ⇒ component. Validation that's local to one form ⇒ Validator. |
| **Composition** (client) | UI that fuses ≥2 kits into one widget (UnifiedInbox = chat unread + notif unread; HomeShell = listings recommended + active subscription badge) | Single-kit UI ⇒ that kit's page. |
| **`<Kit>PageBundle`** | Kit ships ≥1 routable page | Kit is purely an OAM/Operations kit (Cache, Files internals). |
| **App-only page** in `<App>.Customer.UI/Pages/` | App-specific copy (terms, about, marketing landing) | Domain content (browsing listings, profile editing) — that's a kit page. |
| **`IRichTextSanitizer`** | Any user-supplied or server-supplied text rendered as more than plain text | Plain text from a typed `record` field — Blazor's default `@text` is already safe. |
| **`IUrlAllowlist`** | Binding `<img src>` / `<a href>` to a URL that came from JSON/storage | Static asset path (`/assets/logo.png`) — no allowlist needed. |

### Frontend law: pages talk to interfaces, never to entities or HTTP

Kit pages depend on **`IXxxStore` interfaces only**. They do not import
`HttpClient`, never reference an app's domain entity, never call EF, never
build URLs by hand. The store interface is the single integration seam:

```csharp
// libs/kits/Listings/Frontend/Customer/Pages/AcListingExplorePage.razor
@inject IListingsStore Store
@foreach (var l in Store.Visible) { … }   // l is IListing — interface only
```

The app implements `IListingsStore` against whatever data shape it uses
(EF entity, REST DTO, GraphQL fragment) and registers it once via
`AddDomainBindings(b => b.Use<IListingsStore, EjarListingsStore>())`. Reuse
across apps becomes free — change the binding, keep the pages.

### XSS prevention is structural, not optional

Two laws, both enforced at the type level:

1. **Never `MarkupString`.** If you find yourself reaching for it, either
   (a) the text is plain — use `@text` (Blazor escapes by default), or
   (b) it's rich — pass it through `IRichTextSanitizer.Sanitize(...)` first.
   Templates that violate this fail review on principle, not on a CVE.

2. **Never bind external URLs without `IUrlAllowlist`.** The platform's
   `ClientHostBuilder.UseUrlAllowlist(a => a.Add("cdn.example.com"))`
   registers the allowlist. Components that render `<img src>` from
   server-supplied URLs call `Allowlist.IsAllowed(url)` and fall back to
   a placeholder when the host isn't whitelisted.

The default `IRichTextSanitizer` strips ALL HTML — apps that want
markdown register `MarkdownSanitizer` (Markdig + bleach config) explicitly.
This is the OWASP A03 (Injection) defence at the framework level rather
than per-page diligence.

### Frontend layout (parallel to backend ServiceHost)

```
Apps/<X>/Customer/
├── Domain/                     pure entities (implement kit interfaces)
├── Domain.Data/                EF mappings only
├── Backend/<X>.Api/            Program.cs uses ServiceHost
├── Shared/<X>.Customer.UI/     AddXCustomer() — Bindings, Branding, Nav
└── Frontend/
    ├── <X>.Web/                Program.cs ≈ 50 lines (HttpClient + AddXCustomer)
    └── <X>.Maui/               MauiProgram.cs ≈ 50 lines

libs/
├── core/                       OperationEngine + SharedKernel
├── host/                       ACommerce.ServiceHost (backend)
├── host-client/                ACommerce.ClientHost (frontend)
├── kits/<Kit>/
│   ├── Operations/             pure-OAM domain
│   ├── Backend/                Controllers + IXxxStore (server-side port)
│   └── Frontend/Customer/      Pages + IXxxStore (client-side reactive) + PageBundle
├── providers/                  infra adapters
├── templates/                  cross-kit UI shells
└── compositions/               cross-kit interceptor + UI bundles
```

## Tool-call discipline — preventing "انتهاء زمن الاتصال"

Historical analysis of all sessions in this project identified three failure
patterns that caused session crashes or "connection timeout" errors. Rules to
avoid repeating them:

### Rule T1 — Always Read before Edit

**Every single Edit failure** across all sessions was `"File has not been read
yet"`, regardless of file size. Edit with old_string of 300 chars fails just
as surely as one with 5000 chars if the file was not Read first.

```
WRONG:  Edit file X   ← no prior Read → is_error: true
RIGHT:  Read file X → Edit file X  ← always works
```

When editing multiple files in one turn: Read ALL of them first in parallel,
THEN issue the Edits.

### Rule T2 — Never run server processes via Bash

`dotnet run`, `dotnet watch`, and anything that binds a port is killed
immediately by the sandbox with **exit code 144** (seccomp SIGSYS). It never
starts; it never times out. Use only:

```bash
dotnet build  # verify compilation
dotnet test   # run tests
curl          # call an already-running API
```

If a server needs to be running, it must be started outside this session.

### Rule T3 — Keep individual response turns small

The "انتهاء زمن الاتصال" error is a client-side streaming timeout triggered
when a single assistant turn generates too much content (many large tool calls
+ long explanatory text combined). Largest observed successful Write/Edit:
sz=13,746 chars — the tool itself has no size limit. The limit is the **total
volume of one turn**.

To stay safe:
- Do not batch more than ~5 large file writes into a single assistant turn.
- For tasks touching 10+ files, break the work across multiple turns.
- After each batch: build → verify → commit → then continue.

### Rule T4 — Prefer Edit over Write for existing files

`Edit` sends only the changed slice; `Write` sends the full file content.
For files already in the repo, always use `Edit`. Use `Write` only for brand-new
files or when a complete rewrite is genuinely simpler than a patch.

### Rule T6 — Server-returned operation type ≠ client-dispatched type (recurring login trap)

This bug has broken login silently in at least three separate sessions. Understand it
**before** writing any `IOperationInterpreter`.

**The trap:** `ClientOpEngine` merges the server's `OperationEnvelope` back into the
client operation using `OperationMerger.Merge()`. Merge copies tags and parties — but
**NOT** the `Type` field. The `envelope.Operation.Type` the interpreter receives is the
**server-returned** type, not the client-dispatched type.

Apps with role-scoped backends consistently do this:
- Client dispatches `auth.sms.verify` (generic)
- Server returns `auth.admin.sms.verify`, `auth.vendor.sms.verify`, etc. (scoped)

If `CanInterpret` only matches the generic client type, it silently skips the envelope
and the store is never populated. Login appears to succeed (no error) but nothing changes.

**The fix — always match both sides:**

```csharp
// WRONG — only matches the client-dispatched type:
public bool CanInterpret(OperationDescriptor op) =>
    op.Type is "auth.sms.verify";

// RIGHT — match the server-returned type too:
public bool CanInterpret(OperationDescriptor op) =>
    op.Type is "auth.sms.verify" or "auth.admin.sms.verify";

// The switch/case must also handle both:
case "auth.sms.verify":
case "auth.admin.sms.verify":
    // populate store ...
```

**Every time you add a new interpreter or a new operation type on the backend, verify
the server's actual returned `Type` value (log it or check the backend route) and make
sure `CanInterpret` covers it.**

### Rule T5 — Backend port goes in appsettings.Development.json, not .env

`.env.Development` files are NOT loaded automatically by `WebApplication.CreateBuilder`.
Only `appsettings.json` and `appsettings.{Environment}.json` are loaded automatically.

**Every new backend must declare its port in `appsettings.Development.json`:**
```json
{
  "Urls": "http://localhost:XXXX"
}
```

The corresponding frontend must set the same URL in its own `appsettings.json`:
```json
{
  "SomeApi": { "BaseUrl": "http://localhost:XXXX" },
  "Urls": "http://localhost:YYYY"
}
```

**Startup procedure for every new app pair:**
1. Start the backend: `dotnet run --project Apps/.../Backend/...`
2. Confirm the port from the startup log: `Now listening on: http://localhost:XXXX`
3. Verify the frontend's `appsettings.json` → `BaseUrl` matches that port
4. Start the frontend: `dotnet run --project Apps/.../Frontend/...`

If the ports diverge (e.g. backend starts on 5000 instead of the expected port),
the cause is almost always a missing `"Urls"` in `appsettings.Development.json`.
Fix it there — do NOT rely on `.env.Development` alone.

### Rule T7 — Register every new .csproj in ACommerce.Platform.sln

`dotnet new classlib` and writing a `.csproj` directly do NOT add the project
to the solution file. `dotnet build` on the CLI still works (it follows
`<ProjectReference>` regardless of the .sln), so the omission is invisible
to anyone using the CLI. But Visual Studio on Windows fails immediately:

```
NU1105: Unable to find project information for 'C:\...\<NewProject>.csproj'.
The project may be unloaded or not part of the current solution.
```

**Always run `dotnet sln add` immediately after creating a new csproj**,
before adding `<ProjectReference>` consumers.

```bash
dotnet sln ACommerce.Platform.sln add \
  libs/kits/<NewKit>/<Project1>/<Project1>.csproj \
  libs/kits/<NewKit>/<Project2>/<Project2>.csproj \
  --solution-folder "libs/kits/<NewKit>"
```

`--solution-folder` groups them under a Solution Folder so VS shows them
nested correctly; without it they pile in the root.

**Verification** (run before any commit that adds projects):

```bash
diff <(find . -name "*.csproj" -not -path "./bin/*" -not -path "*/bin/*" \
              -not -path "*/obj/*" -not -path "./.git/*" \
       | sed 's|^\./||' | sort) \
     <(grep -oP '(?<=, ")[^"]+\.csproj' ACommerce.Platform.sln | tr '\\' '/' | sort)
```

Lines prefixed `<` = csproj on disk but NOT in the .sln (you forgot
`dotnet sln add`). Lines prefixed `>` = csproj in the .sln but file
deleted (you forgot `dotnet sln remove`). Both indicate broken state;
fix before committing.

This is documented in detail in **`docs/PITFALLS.md` → P13**.

## Boundaries

**Do freely**: read any file; add entities/controllers/services/pages/tests/docs;
create libraries under `libs/`; build and run locally.

**Ask first**: push to main; create PR; force-push; delete files you didn't
create; modify root `.gitignore`/`LICENSE`/`README.md`; add large packages;
add production providers (payment/SMS/firebase/cloud storage).

## The canonical mental model

> "If I can't express this feature as one operation with one sender, one
> receiver, one tag set, and one execute body, I'm modelling it wrong."

Every temptation to add a service class, event bus, or background job — ask
first: is this an interceptor on an existing operation? A child operation
inside a parent's execute body? A provider contract?

## Language

Work in Arabic. Keep code identifiers, file paths, function names, and log
output in English.
