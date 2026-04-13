using ACommerce.SharedKernel.Abstractions.Repositories;
using Vendor.Api.Entities;

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
        if ((await _settings.ListAllAsync(ct)).Any()) return;

        var now = DateTime.UtcNow;

        // ─── VendorUser records (matching Order.Api User IDs + phone numbers) ──
        await _users.AddAsync(new VendorUser
        {
            Id = UserIds.VendorAhmed, CreatedAt = now,
            PhoneNumber = "+966501111111", FullName = "أحمد - كافيه السعادة",
            Role = "vendor", IsActive = true
        }, ct);
        await _users.AddAsync(new VendorUser
        {
            Id = UserIds.VendorFatimah, CreatedAt = now,
            PhoneNumber = "+966502222222", FullName = "فاطمة - مطعم الأصيل",
            Role = "vendor", IsActive = true
        }, ct);
        await _users.AddAsync(new VendorUser
        {
            Id = UserIds.VendorSaad, CreatedAt = now,
            PhoneNumber = "+966503333333", FullName = "سعد - حلويات الرياض",
            Role = "vendor", IsActive = true
        }, ct);
        await _users.AddAsync(new VendorUser
        {
            Id = UserIds.VendorLama, CreatedAt = now,
            PhoneNumber = "+966504444444", FullName = "لمى - عصائر كول بايتس",
            Role = "vendor", IsActive = true
        }, ct);

        // ─── VendorSettings + WorkSchedule ─────────────────────────────────────
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
