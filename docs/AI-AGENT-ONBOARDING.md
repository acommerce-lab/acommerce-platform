# AI agent onboarding

You are an AI coding agent joining this project mid-stream. This document
is your 5-minute brief. Read it before touching any code. It assumes you
are Claude, Codex, Gemini, or similar — the terms are all the same.

---

## What this project is

A .NET 9 / Blazor Web App platform for building **production-ready
multi-vendor e-commerce applications in days, not months**. The stack has
three pillars:

1. **OperationEngine** — a double-entry-accounting kernel where every
   meaningful state change is an `Entry.Create(...).From(...).To(...)
   .Tag(...).Execute(...)` pipeline. Read
   `docs/ACCOUNTING-PHILOSOPHY.md` before writing a single controller.
2. **Client wire format** — every backend response is
   `OperationEnvelope<T>`. Every frontend deserialises into the same
   shape.
3. **Widgets + Templates cascade** — one widgets library with a
   Bootstrap compatibility layer so any Razor page themes automatically
   from a consuming app's `:root` `--ac-*` variable override. Read
   `docs/TEMPLATES-ROADMAP.md` for the honest state + what's missing.

Two full demo apps exist and both work end-to-end:

- **Ashare.Api2 + Ashare.Web2** — property classifieds with subscriptions
  and quota-gated listings.
- **Order.Api2 + Order.Web2** — cafe/restaurant deals with in-store and
  curbside pickup, cash change calculator, no payment provider.

There is also a third, unrelated research project:

- **MagneticLM** — a graph-based language model (`Examples/ACommerce.MagneticLM`) that hit 14.20 PPL on WikiText-103 with no neural components. Read `Examples/ACommerce.MagneticLM/RESEARCH-NOTES.md` for the full research log.

---

## Read these in order before writing code

1. **`docs/ARCHITECTURE.md`** — the library map. Which library lives
   where, what it does, what you can skip.
2. **`docs/ACCOUNTING-PHILOSOPHY.md`** — the OpEngine mental model.
   Without this you will write controllers that bypass the engine and
   the whole point of the platform is lost.
3. **`docs/BUILDING-A-BACKEND.md`** — the practical recipe. It's
   written as copy-paste friendly chunks.
4. **`docs/BUILDING-A-FRONTEND.md`** — same for Blazor.
5. **`docs/TEMPLATES-ROADMAP.md`** — what's missing and what to build
   next if the user wants more productivity.

The two demo apps are your concrete reference:

- Read `Apps/Order.Api2/Program.cs` (~170 lines) for a clean backend
  wiring example.
- Read `Apps/Order.Api2/Controllers/OrdersController.cs` for a clean
  accounting operation example.
- Read `Apps/Order.Web2/Components/App.razor` for the CSS cascade wire-up.
- Read `Apps/Order.Web2/Components/Pages/Checkout.razor` for a complex
  page that uses both widgets and the compat layer.
- Read `Apps/Order.Web2/wwwroot/app.css` for a brand override.

---

## The five laws of this codebase

### Law 1 — Every state change is an operation

Never write this:

```csharp
[HttpPost]
public async Task<IActionResult> Create([FromBody] Dto req)
{
    await _repo.AddAsync(entity);
    return Ok(entity);
}
```

Always write this:

```csharp
[HttpPost]
public async Task<IActionResult> Create([FromBody] Dto req, CancellationToken ct)
{
    var op = Entry.Create("thing.create")
        .From($"User:{req.OwnerId}", 1, ("role", "owner"))
        .To($"Thing:{entity.Id}",    1, ("role", "created"))
        .Tag("name", req.Name)
        .Execute(async ctx => await _repo.AddAsync(entity, ctx.CancellationToken))
        .Build();

    var envelope = await _engine.ExecuteEnvelopeAsync(op, entity, ct);
    if (envelope.Operation.Status != "Success") return BadRequest(envelope);
    return this.OkEnvelope("thing.create", entity);
}
```

The operation gives you audit, interceptors, analyzers, and the
structured envelope for free. A repository call alone gives you none
of those.

### Law 2 — Every response is an OperationEnvelope

Read endpoints too:

```csharp
[HttpGet]
public async Task<IActionResult> List(CancellationToken ct)
{
    var all = await _repo.GetAllWithPredicateAsync(e => !e.IsDeleted);
    return this.OkEnvelope("thing.list", all);
}
```

The envelope contains `.Data`, `.Operation` (type, status,
description, timestamps), and `.Error`. The frontend reads all three
uniformly.

### Law 3 — Use the repository method names correctly

The `IBaseAsyncRepository<T>` interface has two "list" methods that
look similar but are different:

- `ListAllAsync(ct)` — takes a `CancellationToken`, returns everything.
- `GetAllWithPredicateAsync(predicate, includeDeleted = false, params string[] includes)` — does **not** take a `CancellationToken`. The second argument is a `bool`.

Mixing them up is the most common bug when writing a new controller.
If the compiler says "cannot convert CancellationToken to bool", you
passed `ct` to `GetAllWithPredicateAsync` and you meant `ListAllAsync`.

### Law 4 — The widgets cascade is the styling system

When adding a new Razor page:

1. Use either `<AcButton>` or `<button class="btn btn-primary">`. Both
   work because of the compat layer.
2. Use either `.ac-card` or `.card`. Both work.
3. Never add hard-coded colours. Always reference `var(--ac-primary)`
   or use a utility class like `text-primary`.
4. Never add `<link>` tags to external CDNs. The sandbox blocks most
   of them. Everything should be served from `_content/` or self-hosted
   in `wwwroot/lib/`.

The consumer app overrides `:root` variables in its own `app.css`.
That is the **only** place brand colours should appear.

### Law 5 — Auth state must survive a page reload

Every page that reads `Auth.IsAuthenticated` must do its data loading
in `OnAfterRenderAsync(firstRender: true)` and must call
`await Auth.EnsureRestoredAsync()` there. Putting auth-dependent
logic in `OnInitializedAsync` will show a logged-out UI on reload
even for a logged-in user, because `ProtectedLocalStorage` is a JS
interop call and JS is not available during the initial render.

See `Apps/Order.Web2/Components/Pages/MyOrders.razor` for the
canonical pattern.

---

## How to plan a new feature

When the user asks for a feature, follow this sequence:

1. **Identify the operation.** What state change is this? Who is the
   sender? Who is the receiver? What's the unit of value? What tags
   apply? What analyzers should gate it?
2. **Identify the entities.** What needs to be stored? Does an
   existing entity cover it, or do you need a new one?
3. **Identify the cross-cutting concerns.** Does this need a quota
   check? A permission check? An audit record? A notification? Each
   of these is an interceptor, not a controller-side `if`.
4. **Write the entity** in `Entities/`, register it in `Program.cs`
   via `EntityDiscoveryRegistry.RegisterEntity`.
5. **Write the controller method** that builds the operation and
   calls `_engine.ExecuteEnvelopeAsync`.
6. **If needed, write or register an interceptor** in `Program.cs`
   `AddOperationInterceptors`.
7. **Seed data** for the feature in the seeder.
8. **Write the frontend page** using the widgets cascade. Start from
   an existing page that has the closest shape and adapt.
9. **Test** by running both services (`run-local.sh` in Order.Web2 or
   the equivalent in Ashare.Web2) and using Playwright via the scripts
   in `/tmp/pwtest/` to click through the flow.

---

## How to not get stuck

- If a build error says "the type or namespace X does not exist",
  check the csproj for the right `<ProjectReference>`. Most of the
  libraries you need are under `libs/backend/core/`, `libs/backend/auth/`,
  or `libs/frontend/`. Copy the reference from Order.Api2 or
  Order.Web2.
- If a runtime error says "SQLite Error 1: no such table: …", you
  forgot to call `db.Database.EnsureCreatedAsync()` at startup.
  See `Apps/Order.Api2/Program.cs`'s bottom 20 lines.
- If the Web app doesn't respond to button clicks, check that
  `<Routes @rendermode="@RenderMode.InteractiveServer" />` is set on
  `App.razor` and that `blazor.web.js` serves a 200 from
  `wwwroot/_framework/`.
- If the CSS looks wrong, check that `widgets.css` is linked **before**
  `templates.css` **before** `app.css` in `App.razor`. The order is
  the cascade.
- If your backend controllers return 401 for everything, you probably
  forgot `app.UseAuthentication()` before `app.UseAuthorization()`.

---

## Boundaries

Don't do these without explicit user approval:

- Push to `main` or any branch other than the one the user specified.
- Create a pull request.
- Amend or force-push existing commits.
- Delete files the user created.
- Modify `.gitignore`, `LICENSE`, `README.md` at the repo root (but
  `docs/*.md` and `Apps/*/README.md` are fine).
- Install new top-level NuGet packages without a good reason.
- Add a new database provider (keep SQLite for preview).
- Wire in a payment gateway, a real SMS provider, firebase, etc. —
  these are user-approval gates.

---

## Boundaries (continuation)

Do these freely:

- Read any file.
- Write new entities, controllers, services, Razor components in an
  existing app.
- Write new libraries under `libs/`.
- Write new tests.
- Write new documentation.
- Create new Razor pages in Order.Web2, Ashare.Web2, or any similar
  existing app.
- Build and run the apps locally to verify.
- Use Playwright to screenshot your work.
- Commit to the working branch.

---

## The current branch

The branch the user is working on is
`claude/local-dotnet-build-testing-b5DgA`. All commits land there
until the user explicitly asks for a different branch.

---

## The canonical mental model

> **"If I can't express this feature as one operation, with one sender,
> one receiver, one tag set, and one execute body, I'm modelling it wrong."**

Every time you feel tempted to add a service class, an event bus, a
background job, a scheduled task — ask first: can this be an
interceptor on an existing operation? Can it be a child operation
inside a parent operation's execute body? Can it be a separate
operation triggered by a domain event?

The engine is the thinking tool. The libraries are sharp but narrow.
The apps are thin leaves. That's the whole platform.

---

## Magnetic LM (the research project, separate from the platform)

`Examples/ACommerce.MagneticLM/` is a separate research track. It is
NOT part of the e-commerce platform. It is a graph-based language
model that hit 14.20 perplexity on WikiText-103 using only n-grams
and a 3D physics simulation (no neural networks).

If the user asks you to work on it, read
`Examples/ACommerce.MagneticLM/RESEARCH-NOTES.md` first. It contains
the full state, current results, open questions, and the next
experiments to try. Do not mix this code with the platform code.

If the user wants to split it into its own repository, the script
`scripts/split-magneticlm.sh` does exactly that (using
`git subtree split` so history is preserved).
