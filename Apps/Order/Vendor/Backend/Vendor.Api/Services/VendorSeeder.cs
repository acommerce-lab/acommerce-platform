using ACommerce.SharedKernel.Abstractions.Repositories;
using Vendor.Api.Entities;
using ACommerce.OrderPlatform.Entities;

namespace Vendor.Api.Services;

public class VendorSeeder
{
    // Must match Order.Api/Services/OrderSeeder.cs VendorIds exactly
    public static class VendorIds
    {
        public static readonly Guid HappinessCafe  = Guid.Parse("20000000-0000-0000-0001-000000000001");
        public static readonly Guid AlAseelKitchen = Guid.Parse("20000000-0000-0000-0001-000000000002");
        public static readonly Guid RiyadhSweets   = Guid.Parse("20000000-0000-0000-0001-000000000003");
        public static readonly Guid CoolBites      = Guid.Parse("20000000-0000-0000-0001-000000000004");
    }

    // Must match Order.Api/Services/OrderSeeder.cs UserIds exactly
    public static class UserIds
    {
        public static readonly Guid VendorAhmed   = Guid.Parse("00000000-0000-0000-0002-000000000001");
        public static readonly Guid VendorFatimah = Guid.Parse("00000000-0000-0000-0002-000000000002");
        public static readonly Guid VendorSaad    = Guid.Parse("00000000-0000-0000-0002-000000000003");
        public static readonly Guid VendorLama    = Guid.Parse("00000000-0000-0000-0002-000000000004");
    }

    private readonly IBaseAsyncRepository<VendorUser> _users;
    private readonly IBaseAsyncRepository<VendorSettings> _settings;
    private readonly IBaseAsyncRepository<WorkSchedule> _schedules;

    public VendorSeeder(IRepositoryFactory f)
    {
        _users = f.CreateRepository<VendorUser>();
        _settings = f.CreateRepository<VendorSettings>();
        _schedules = f.CreateRepository<WorkSchedule>();
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        // ─── VendorUser records — AGGRESSIVE: delete all demo-phone users, then recreate ──
        // Why: over time, AuthController auto-creates users with random IDs when
        // it can't find them by phone for any reason. This guarantees that on
        // every startup, demo users have the CORRECT ID regardless of what
        // happened in previous sessions.
        var demoUsers = new[]
        {
            (UserIds.VendorAhmed,   "+966501111111", "أحمد - كافيه السعادة"),
            (UserIds.VendorFatimah, "+966502222222", "فاطمة - مطعم الأصيل"),
            (UserIds.VendorSaad,    "+966503333333", "سعد - حلويات الرياض"),
            (UserIds.VendorLama,    "+966504444444", "لمى - عصائر كول بايتس"),
        };
        var demoPhones = demoUsers.Select(u => u.Item2).ToHashSet();

        var allExisting = await _users.ListAllAsync(ct);
        foreach (var u in allExisting.Where(x => demoPhones.Contains(x.PhoneNumber)))
        {
            await _users.DeleteAsync(u, ct);
        }

        foreach (var (id, phone, name) in demoUsers)
        {
            await _users.AddAsync(new VendorUser
            {
                Id = id, CreatedAt = now,
                PhoneNumber = phone, FullName = name,
                Role = "vendor", IsActive = true
            }, ct);
        }

        // ─── VendorSettings + WorkSchedule — only if no settings exist yet ──
        if ((await _settings.ListAllAsync(ct)).Any()) return;
        var vendors = new[] { VendorIds.HappinessCafe, VendorIds.AlAseelKitchen, VendorIds.RiyadhSweets, VendorIds.CoolBites };
        var configs = new[]
        {
            ("07:00", "23:00", 10), // Happiness Café
            ("11:00", "23:30", 15), // Al-Aseel Kitchen
            ("09:00", "01:00", 10), // Riyadh Sweets
            ("09:00", "00:00", 8),  // Cool Bites
        };

        for (int i = 0; i < vendors.Length; i++)
        {
            var vid = vendors[i];
            var (open, close, timeout) = configs[i];

            await _settings.AddAsync(new VendorSettings
            {
                Id = Guid.NewGuid(),
                CreatedAt = now,
                VendorId = vid,
                AcceptingOrders = true,
                MaxConcurrentPending = 5,
                OrderTimeoutMinutes = timeout,
            }, ct);

            for (int d = 0; d < 7; d++)
            {
                await _schedules.AddAsync(new WorkSchedule
                {
                    Id = Guid.NewGuid(),
                    CreatedAt = now,
                    VendorId = vid,
                    DayOfWeek = (DayOfWeek)d,
                    OpenTime = open,
                    CloseTime = close,
                    IsOff = d == 5, // Friday off for demo
                }, ct);
            }
        }
    }
}
