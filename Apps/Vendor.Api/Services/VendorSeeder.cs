using ACommerce.SharedKernel.Abstractions.Repositories;
using Vendor.Api.Entities;

namespace Vendor.Api.Services;

public class VendorSeeder
{
    // These GUIDs match Order.Api/Services/OrderSeeder.cs exactly
    public static class VendorIds
    {
        public static readonly Guid HappinessCafe = Guid.Parse("00000000-0000-0000-0003-000000000001");
        public static readonly Guid AlAseelKitchen = Guid.Parse("00000000-0000-0000-0003-000000000002");
        public static readonly Guid RiyadhSweets = Guid.Parse("00000000-0000-0000-0003-000000000003");
        public static readonly Guid CoolBites = Guid.Parse("00000000-0000-0000-0003-000000000004");
    }

    private readonly IBaseAsyncRepository<VendorSettings> _settings;
    private readonly IBaseAsyncRepository<WorkSchedule> _schedules;

    public VendorSeeder(IRepositoryFactory f)
    {
        _settings = f.CreateRepository<VendorSettings>();
        _schedules = f.CreateRepository<WorkSchedule>();
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        if ((await _settings.ListAllAsync(ct)).Any()) return;

        var now = DateTime.UtcNow;
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

            // 7 days of the week
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
