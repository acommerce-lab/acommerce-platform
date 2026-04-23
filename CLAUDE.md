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
6. **`docs/ROADMAP.md`** — what's done, what's next, modification plan.

Reference apps (read the smallest first):
- `Apps/Order.Api` + `Apps/Order.Web` — cafe deals, cleanest example.
- `Apps/Vendor.Api` + `Apps/Vendor.Web` — vendor-side microservice.
- `Apps/Ashare.Api` + `Apps/Ashare.Web` — property classifieds, larger.

## The five laws

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

### Law 6 — Adapt to real data, never bend it

When integrating with an existing production system, the NEW platform adapts
to the shape of production data — not the other way around. If the
stakeholder's listings use `features` instead of `amenities`, rename the
template. If the production API returns a plain array instead of
OperationEnvelope, handle both. Any attribute key not in the template is
preserved as a raw `DynamicAttribute` entry. We serve the stakeholder's
existing data exactly as they expect it; the platform is a tool, not an
authority over business data.

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
