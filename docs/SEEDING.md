# Data Seeding Contract

## The Problem

Each Order service (Customer Api, Vendor Api, Admin Api) has its own SQLite
database. Demo users are defined separately in each seeder. When the seeders
get out of sync — or when a vendor auto-registers via OTP before the seeder
has run — the user IDs in Order.Api's `Vendor.OwnerId` don't match the IDs
in Vendor.Api's `VendorUser.Id`. Result: vendor login works but the vendor
can't see their own store because the lookup `v.OwnerId == currentUser.Id`
returns nothing.

Same problem applies to Ashare: Ashare.Api, Ashare.Provider.Api,
Ashare.Admin.Api.

## The Two Solutions

### Option A — Coordinated seeders (currently implemented)

Each API has its own database, but uses the **same hard-coded demo user IDs**:

```csharp
// Both Order.Api OrderSeeder and Vendor.Api VendorSeeder share:
public static readonly Guid VendorAhmed = Guid.Parse("00000000-0000-0000-0002-000000000001");
```

The VendorSeeder enforces ID correctness:
```csharp
foreach (var (id, phone, name) in demoUsers)
{
    var existing = existingUsers.FirstOrDefault(u => u.PhoneNumber == phone);
    if (existing != null && existing.Id == id) continue;       // correct → skip
    if (existing != null && existing.Id != id)
        await _users.DeleteAsync(existing, ct);                // wrong ID → delete
    await _users.AddAsync(new VendorUser { Id = id, ... }, ct); // create correct
}
```

**Pros**: No schema coupling between services. Each service owns its data.
**Cons**: Need to keep IDs in sync manually.

### Option B — Unified database per platform (optional, requires refactor)

All three Order services point to the same SQLite file: `data/order-shared.db`.
Each service creates its own tables (no name conflicts) in that shared file.

To enable, set the environment variable in each Order API's launchSettings:
```json
{
  "environmentVariables": {
    "Database__ConnectionString": "Data Source=../../../../../data/order-shared.db"
  }
}
```

Or in appsettings.json:
```json
{
  "Database": {
    "ConnectionString": "Data Source=C:/dev/acommerce-platform/data/order-shared.db"
  }
}
```

**Pros**: Only one seeder (e.g. Order.Api's) creates demo users. Vendor.Api
queries the same User table. No ID duplication possible.
**Cons**: Requires refactoring Vendor.Api to use Order.Api's `User` entity
instead of its own `VendorUser`. This is a bigger change; Option A is
sufficient in most cases.

## Demo Credentials (Constant Across All Services)

### Order Platform
| Role | Phone | UserId | Entity |
|------|-------|--------|--------|
| Customer (Sara) | +966500000001 | `00000000-0000-0000-0001-000000000001` | Order.Api.User |
| Vendor (Ahmed — Happiness Café) | +966501111111 | `00000000-0000-0000-0002-000000000001` | Order.Api.User + Vendor.Api.VendorUser |
| Vendor (Fatimah — Al-Aseel Kitchen) | +966502222222 | `00000000-0000-0000-0002-000000000002` | ... |
| Vendor (Saad — Riyadh Sweets) | +966503333333 | `00000000-0000-0000-0002-000000000003` | ... |
| Vendor (Lama — Cool Bites) | +966504444444 | `00000000-0000-0000-0002-000000000004` | ... |

### Vendor Entities (Order.Api)
| Vendor | Id | OwnerId |
|--------|----|---------| 
| Happiness Café (كافيه السعادة) | `20000000-0000-0000-0001-000000000001` | `00000000-0000-0000-0002-000000000001` |
| Al-Aseel Kitchen | `20000000-0000-0000-0001-000000000002` | `00000000-0000-0000-0002-000000000002` |
| Riyadh Sweets | `20000000-0000-0000-0001-000000000003` | `00000000-0000-0000-0002-000000000003` |
| Cool Bites | `20000000-0000-0000-0001-000000000004` | `00000000-0000-0000-0002-000000000004` |

## The Testing Workflow

1. **Stop all running services** (close Visual Studio debug sessions)
2. **Delete DB files**:
   ```powershell
   Remove-Item "Apps\Order\Customer\Backend\Order.Api\data\order.db"  -ErrorAction SilentlyContinue
   Remove-Item "Apps\Order\Vendor\Backend\Vendor.Api\data\vendor.db"  -ErrorAction SilentlyContinue
   ```
3. **Rebuild**: `dotnet build ACommerce.Platform.sln`
4. **Start 3 services simultaneously** (multiple startup projects in VS):
   - Order.Api (port 5101)
   - Vendor.Api (port 5201)
   - Vendor.Web (port 5801)
5. **Verify seeder ran**: Check Vendor.Api console shows:
   ```
   Added VendorUser with id 00000000-0000-0000-0002-000000000001
   ```
   NOT a random UUID like `ed73f037-...`
6. **Login** with +966501111111 → code appears in Vendor.Api console
7. **Home page** should show "Happiness Café" dashboard

## The Browser-Side Gotcha

Auth state persists in browser's `ProtectedLocalStorage`. If you test, logout,
delete DB, restart, and login again — the browser may still try to use the
OLD token from a PREVIOUS DB. Symptoms: "logged in" but no data loads.

**Fix**: Always sign out BEFORE deleting the database, OR clear browser
localStorage for `localhost:5801` before testing again.

---

## Production API Backfill (Ashare)

### The Approach

Instead of migrating from a database, the Ashare seeder fetches real listings
from the production API (`api.ashare.sa`) at every startup. This means:

- `SqliteSchemaGuard` deletes and recreates the DB normally on schema drift.
- The seeder seeds local categories + users first, then calls the production API.
- Production listings are added alongside local seed data (deduplicated by ID).
- If the API is unreachable, the seeder falls back to hardcoded local data.

### Why Not a Migrator Tool?

We built `tools/AshareMigrator/` (a console app that reads SQL Server and
writes to SQLite) but abandoned it because:

1. Required manual file copying between the migrator output and Ashare.Api.
2. `EnsureCreatedAsync` is all-or-nothing — doesn't create missing tables.
3. `SqliteSchemaGuard` deletes the file on schema drift, losing migrated data.
4. The seeder approach is zero-config: just run Ashare.Api and it pulls data.

### Handling Production Data Shape

The production API returns a **plain JSON array** (no OperationEnvelope wrapper):

```json
[
  {
    "id": "...",
    "vendorId": "...",       // maps to OwnerId
    "images": ["url1", ...], // native array, not JSON string
    "attributes": { ... },   // native object, not JSON string
    "status": "Active",      // string, not int
    ...
  }
]
```

The seeder's `MapListing` handles all format differences:

- `images` array → `ImagesCsv` (comma-separated)
- `attributes` object → `DynamicAttributesJson` (snapshot via template)
- `status` string → `ListingStatus` enum
- Entity-level fields (`is_phone_allowed`, `license_number`, `duration`) are
  extracted from inside the `attributes` object where the production stores them.

### The "No Data Cut" Rule

Any attribute key from the production `attributes` object that doesn't match
a template field is preserved as a raw `DynamicAttribute` with an inferred type.
The new system adapts to the stakeholder's data shape, never the reverse.

### `JsonElement.TryGetProperty` Safety

`TryGetProperty` **throws** `InvalidOperationException` on array elements
instead of returning false. Always check `ValueKind != JsonValueKind.Object`
before calling it. This caught us three separate times during development.

### Ashare Demo Credentials

| Role | Phone | UserId |
|------|-------|--------|
| Owner (Ahmed) | +966500000001 | `00000000-0000-0000-0001-000000000001` |
| Customer (Sara) | +966500000002 | `00000000-0000-0000-0001-000000000002` |
| Admin | +966500000003 | `00000000-0000-0000-0001-000000000003` |

Production owners are created as placeholder `User` entities with
`Role = "owner"` and their production `vendorId` as the user ID.
