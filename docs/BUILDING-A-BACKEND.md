# Building a backend service

Step-by-step recipe for a new backend app on this stack, using
`Apps/Order.Api` as the canonical reference. By the end of this guide you
will have a SQLite-backed service with JWT auth, OTP login, a custom domain,
and every mutation going through the accounting engine.

Prerequisites:

- .NET 9 SDK (or .NET 10 with rollForward — the apps here use that)
- Basic familiarity with ASP.NET Core minimal hosting

---

## 1. Create the project

```bash
mkdir -p Apps/MyApp.Api
cd Apps/MyApp.Api
```

Create `MyApp.Api.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <RollForward>LatestMajor</RollForward>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>MyApp.Api</RootNamespace>
    <AssemblyName>MyApp.Api</AssemblyName>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Swashbuckle.AspNetCore" Version="7.0.0" />
    <PackageReference Include="Serilog.AspNetCore" Version="8.0.3" />
    <PackageReference Include="Serilog.Sinks.Console" Version="6.0.0" />
    <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="9.0.0" />
  </ItemGroup>

  <ItemGroup>
    <!-- Core (always) -->
    <ProjectReference Include="..\..\libs\backend\core\ACommerce.SharedKernel.Abstractions\ACommerce.SharedKernel.Abstractions.csproj" />
    <ProjectReference Include="..\..\libs\backend\core\ACommerce.SharedKernel.Infrastructure.EFCores\ACommerce.SharedKernel.Infrastructure.EFCores.csproj" />
    <ProjectReference Include="..\..\libs\backend\core\ACommerce.OperationEngine\ACommerce.OperationEngine.csproj" />
    <ProjectReference Include="..\..\libs\backend\core\ACommerce.OperationEngine.Wire\ACommerce.OperationEngine.Wire.csproj" />

    <!-- Authentication + JWT + SMS 2FA -->
    <ProjectReference Include="..\..\libs\backend\auth\ACommerce.Authentication.Operations\ACommerce.Authentication.Operations.csproj" />
    <ProjectReference Include="..\..\libs\backend\auth\ACommerce.Authentication.Providers.Token\ACommerce.Authentication.Providers.Token.csproj" />
    <ProjectReference Include="..\..\libs\backend\auth\ACommerce.Authentication.TwoFactor.Operations\ACommerce.Authentication.TwoFactor.Operations.csproj" />
    <ProjectReference Include="..\..\libs\backend\auth\ACommerce.Authentication.TwoFactor.Providers.Sms\ACommerce.Authentication.TwoFactor.Providers.Sms.csproj" />

    <!-- Pick whichever of these you need -->
    <!-- <ProjectReference Include="..\..\libs\backend\messaging\ACommerce.Notification.Operations\…" /> -->
    <!-- <ProjectReference Include="..\..\libs\backend\other\ACommerce.Favorites.Operations\…" /> -->
  </ItemGroup>
</Project>
```

Copy `Apps/Order.Api/Order.Api.csproj` as a starting point — it already
has all the right references for a typical app.

---

## 2. Define entities

Every entity implements `IBaseEntity`. Put them under `Entities/`:

```csharp
// Entities/MyEntity.cs
using ACommerce.SharedKernel.Abstractions.Entities;

namespace MyApp.Api.Entities;

public class MyEntity : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    // your fields
    public string Name { get; set; } = default!;
    public decimal Price { get; set; }
    // ...
}
```

That's it. **No data annotations, no EF-specific attributes, no
DbContext**. The `SharedKernel.Infrastructure.EFCores` library
discovers the entity at startup and builds a table for it.

---

## 3. Register entities + wire the DI container

Copy `Apps/Order.Api/Program.cs` as a starting template. The skeleton
to customise is:

```csharp
using ACommerce.OperationEngine.Core;
using ACommerce.SharedKernel.Abstractions.Entities;
using ACommerce.SharedKernel.Infrastructure.EFCores.Extensions;
using MyApp.Api.Entities;
// ... (auth + tfa using lines)

var builder = WebApplication.CreateBuilder(args);

// 1. Register every entity with the discovery registry
EntityDiscoveryRegistry.RegisterEntity(typeof(User));       // your auth user
EntityDiscoveryRegistry.RegisterEntity(typeof(MyEntity));   // your domain entity
EntityDiscoveryRegistry.RegisterEntity(typeof(TwoFactorChallengeRecord));

// 2. Database (SQLite for preview, SQL Server for prod)
var dbProvider = builder.Configuration["Database:Provider"] ?? "SQLite";
var dbConnection = builder.Configuration["Database:ConnectionString"];
switch (dbProvider.ToLowerInvariant())
{
    case "sqlite":
        Directory.CreateDirectory("data");
        builder.Services.AddACommerceSQLite(dbConnection ?? "Data Source=data/myapp.db");
        break;
    case "sqlserver":
        builder.Services.AddACommerceSqlServer(dbConnection!);
        break;
    default:
        builder.Services.AddACommerceInMemoryDatabase("MyAppDb");
        break;
}

// 3. OperationEngine (Scoped so interceptors can consume the request scope)
builder.Services.AddScoped<OpEngine>(sp =>
    new OpEngine(sp, sp.GetRequiredService<ILogger<OpEngine>>()));

// 4. MVC, CORS, Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins("http://localhost:5701")
     .AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

// 5. Authentication: JWT + SMS 2FA (see Apps/Order.Api/Program.cs for the
//    full 40 lines — copy it verbatim and change the Issuer/Audience)

// 6. Your seeder
builder.Services.AddScoped<MyAppSeeder>();

var app = builder.Build();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();

// 7. Create the DB schema + seed on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider
        .GetRequiredService<ACommerce.SharedKernel.Infrastructure.EFCores.Context.ApplicationDbContext>();
    await db.Database.EnsureCreatedAsync();
    await scope.ServiceProvider.GetRequiredService<MyAppSeeder>().SeedAsync();
}

app.Run();
```

Everything marked `// 5. …` is copy-paste from `Apps/Order.Api/Program.cs`.
The only moving parts are the entity list and the database connection.

---

## 4. Write the envelope helpers

Every controller response is an `OperationEnvelope<T>`. Copy
`Apps/Order.Api/Controllers/EnvelopeHelpers.cs` verbatim — it gives you:

- `this.OkEnvelope(opType, data)`
- `this.NotFoundEnvelope("my_not_found")`
- `this.BadRequestEnvelope("code", "message", "hint")`
- `this.UnauthorizedEnvelope()`
- `this.ForbiddenEnvelope()`

Use them everywhere.

---

## 5. Write your first controller (read-only)

```csharp
using ACommerce.SharedKernel.Abstractions.Repositories;
using Microsoft.AspNetCore.Mvc;
using MyApp.Api.Entities;

namespace MyApp.Api.Controllers;

[ApiController]
[Route("api/things")]
public class ThingsController : ControllerBase
{
    private readonly IBaseAsyncRepository<MyEntity> _repo;

    public ThingsController(IRepositoryFactory factory)
    {
        _repo = factory.CreateRepository<MyEntity>();
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var all = await _repo.GetAllWithPredicateAsync(e => !e.IsDeleted);
        return this.OkEnvelope("thing.list", all);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var e = await _repo.GetByIdAsync(id, ct);
        return e == null
            ? this.NotFoundEnvelope("thing_not_found")
            : this.OkEnvelope("thing.get", e);
    }
}
```

Note two things about the repository API:
- `GetAllWithPredicateAsync(predicate)` — the second argument is `bool includeDeleted`, **not** a `CancellationToken`.
- `ListAllAsync(ct)` — takes a `CancellationToken` and returns everything.

Mixing them up is the most common bug when porting a controller.

---

## 6. Write a mutating controller (through the engine)

This is where the accounting model earns its keep. Every mutation goes
through `OpEngine.ExecuteEnvelopeAsync`:

```csharp
using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;
using ACommerce.OperationEngine.Wire;
using ACommerce.SharedKernel.Abstractions.Repositories;
using Microsoft.AspNetCore.Mvc;
using MyApp.Api.Entities;

[ApiController]
[Route("api/things")]
public class ThingsWriteController : ControllerBase
{
    private readonly IBaseAsyncRepository<MyEntity> _repo;
    private readonly OpEngine _engine;

    public ThingsWriteController(IRepositoryFactory factory, OpEngine engine)
    {
        _repo = factory.CreateRepository<MyEntity>();
        _engine = engine;
    }

    public record CreateThing(Guid OwnerId, string Name, decimal Price);

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateThing req, CancellationToken ct)
    {
        var entity = new MyEntity
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            Name = req.Name,
            Price = req.Price,
        };

        var op = Entry.Create("thing.create")
            .Describe($"Thing '{req.Name}' created by User:{req.OwnerId}")
            .From($"User:{req.OwnerId}", 1, ("role", "owner"))
            .To($"Thing:{entity.Id}",    1, ("role", "created_entity"))
            .Tag("name", req.Name)
            .Analyze(new RequiredFieldAnalyzer("name", () => req.Name))
            .Execute(async ctx =>
            {
                await _repo.AddAsync(entity, ctx.CancellationToken);
                ctx.Set("thingId", entity.Id);
            })
            .Build();

        var envelope = await _engine.ExecuteEnvelopeAsync(op, entity, ct);
        if (envelope.Operation.Status != "Success")
            return BadRequest(envelope);
        return this.OkEnvelope("thing.create", entity);
    }
}
```

Three things to notice:

1. The operation has **two parties** (the owner as sender, the entity as
   receiver). This is overkill for a trivial "create" but gets you a
   uniform shape — a later analyzer or interceptor can read the sender
   without the controller knowing.
2. The `Execute` body calls into the repository. This is the **only**
   place side-effects happen. Everything else is declaration.
3. The envelope is returned whole on failure. The client gets the
   structured `OperationError` for free.

---

## 7. Write a seeder

Seeders are just services that run at startup from `Program.cs`. Pattern
(copy from `Apps/Order.Api/Services/OrderSeeder.cs`):

```csharp
public class MyAppSeeder
{
    public static class UserIds
    {
        public static readonly Guid DemoUser = Guid.Parse("00000000-0000-0000-0001-000000000001");
    }

    private readonly IBaseAsyncRepository<User> _users;
    private readonly IBaseAsyncRepository<MyEntity> _things;

    public MyAppSeeder(IRepositoryFactory f)
    {
        _users = f.CreateRepository<User>();
        _things = f.CreateRepository<MyEntity>();
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        if ((await _users.ListAllAsync(ct)).Any()) return;  // already seeded

        await _users.AddAsync(new User
        {
            Id = UserIds.DemoUser,
            CreatedAt = DateTime.UtcNow,
            PhoneNumber = "+966500000001",
            FullName = "Demo User",
            Role = "customer",
            IsActive = true,
        }, ct);

        await _things.AddAsync(new MyEntity
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            Name = "Demo",
            Price = 42m,
        }, ct);
    }
}
```

---

## 8. Register an interceptor (optional but powerful)

If you want a cross-cutting rule, add it to `Program.cs`:

```csharp
using ACommerce.OperationEngine.Interceptors;

builder.Services.AddOperationInterceptors(registry =>
{
    registry.Register(new PredicateInterceptor(
        name: "audit",
        phase: InterceptorPhase.Post,
        appliesTo: op => true,          // every operation
        intercept: async (ctx, _) =>
        {
            var logger = ctx.Services.GetRequiredService<ILogger<Program>>();
            logger.LogInformation(
                "OP {Type} by {User} status={Status}",
                ctx.Operation.Type,
                ctx.Operation.GetPartiesByTag("role","sender").FirstOrDefault()?.Identity,
                ctx.Result.Success ? "Success" : "Failure");
            return InterceptorOutcome.Continue;
        }));
});
```

Now every operation in the system is audit-logged without any
controller code.

---

## 9. Run it

```bash
dotnet build
cd Apps/MyApp.Api
ASPNETCORE_ENVIRONMENT=Development dotnet run
# http://localhost:5xxx/swagger
```

For SQLite you'll see `data/myapp.db` created on first run. Delete it to
reset the DB.

---

## 10. Common pitfalls

- **`RollForward` missing** → error "Framework 'Microsoft.NETCore.App 9.0.0' was not found". Add `<RollForward>LatestMajor</RollForward>` to the csproj.
- **Entity not discovered** → you forgot to call `EntityDiscoveryRegistry.RegisterEntity(typeof(MyEntity))` in Program.cs.
- **`GetAllWithPredicateAsync(p, ct)` fails** → the second argument is `bool includeDeleted`, not a CancellationToken. Use `GetAllWithPredicateAsync(p)` or pass `includeDeleted: false`.
- **`ListAllAsync(ct)`** — this one *does* take the CancellationToken.
- **`OpEngine` is Singleton** → don't. Make it Scoped so its interceptors can consume scoped services like `DbContext`.
- **Forgot `app.UseAuthentication()`** → the `[Authorize]` attributes silently pass. Always add both `UseAuthentication` and `UseAuthorization`.
- **Swagger missing fields** → add `[FromBody]` to POST action parameters.

For anything not covered here, `Apps/Order.Api` is the canonical
reference. Its `Program.cs` is ~170 lines, controllers are ~100 lines
each, and it's the minimum viable shape.
