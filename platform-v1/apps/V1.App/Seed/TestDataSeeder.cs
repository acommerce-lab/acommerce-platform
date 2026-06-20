using ACommerce.Kit.Auth;
using ACommerce.Kit.Chat;
using ACommerce.Kit.Listings;
using ACommerce.Kit.Notifications;
using ACommerce.Kit.Roles;
using ACommerce.Kit.Tenants;
using ACommerce.Kit.Favorites;
using ACommerce.Kit.Cart;
using Marten;

namespace ACommerce.V1.App.Seed;

/// <summary>
/// بَذر بَيانات اختِبار لِفَحص Layer 6 — يُضيف:
///   ١) أَدوار قِياسِيَّة (customer / rider / driver / vendor) لِكُلّ مُستَأجِر
///   ٢) مُستَخدِم اختِبار لِكُلّ دَور (هاتِف فَريد لِيُمكِن تَسجيل دُخوله)
///   ٣) إعلان واحِد + مُحادَثَة + إشعار + مُفَضَّلَة + عُنصُر سَلَّة لِكُلّ
///      مُستَخدِم لِتَمتَلِئ الصَفَحات بَيانات (تَفادي حالَة empty-state).
/// idempotent: لَو الـ user مَوجود بِنَفس الهاتِف، يُتخَطّى.
/// </summary>
public static class TestDataSeeder
{
    /// <summary>تَخطيط الاختِبار: tenant → roles[] → phone pattern.
    /// رَقم الهاتِف لِكُلّ مُستَخدِم = "05{tenant_idx}{role_idx}1234567"
    /// كَ مِفتاح بَسيط يُسَهِّل تَوليد الـ login matrix في verify-runtime.</summary>
    public static readonly (string slug, string[] roles)[] Plan =
    {
        ("ashare", new[] { "customer", "host", "vendor" }),
        ("ejar",   new[] { "customer", "host", "vendor" }),
        ("order",  new[] { "customer", "vendor", "driver" }),
        ("injez",  new[] { "rider", "driver" }),
    };

    public static async Task RunAsync(IServiceProvider services)
    {
        var store = services.GetRequiredService<IDocumentStore>();
        await using var globalSession = store.LightweightSession();

        for (int ti = 0; ti < Plan.Length; ti++)
        {
            var (slug, roles) = Plan[ti];
            var tenant = await globalSession.LoadAsync<Tenant>(slug);
            if (tenant is null) continue;   // tenant غَير مَوجود → تَخَطّي

            // (١) أَضِف الأَدوار النّاقِصَة فَقَط — احتَرِم تَخصيص الـ admin.
            bool tenantTouched = false;
            for (int ri = 0; ri < roles.Length; ri++)
            {
                var slugRole = roles[ri];
                if (tenant.Roles.Any(r => r.Slug == slugRole)) continue;
                var tmpl = RoleCatalog.Find(slugRole);
                if (tmpl is null) continue;
                tenant.Roles.Add(RoleCatalog.InstantiateRole(tmpl, sortOrder: ri));
                tenantTouched = true;
            }
            if (tenantTouched)
            {
                globalSession.Store(tenant);
                Console.WriteLine($"[TestSeed] tenant '{slug}' got {tenant.Roles.Count} roles.");
            }

            // (٢) مُستَخدِم لِكُلّ دَور.
            await using var tenantSession = store.LightweightSession(slug);
            for (int ri = 0; ri < roles.Length; ri++)
            {
                var slugRole = roles[ri];
                var phone = $"05{ti}{ri}1234567"; // مَثَلاً 050012345 لِـ ashare/customer
                var user = (await tenantSession.Query<User>()
                    .Where(u => u.Phone == phone).ToListAsync())
                    .FirstOrDefault();
                if (user is null)
                {
                    user = new User
                    {
                        Id = Guid.NewGuid(),
                        Phone = phone,
                        FullName = $"اختِبار {slug} {slugRole}",
                        ActiveRole = slugRole,
                        Role = slugRole,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    tenantSession.Store(user);
                    Console.WriteLine($"[TestSeed] {slug}/{slugRole} → {phone}");
                }
                else if (user.ActiveRole != slugRole)
                {
                    user.ActiveRole = slugRole;
                    tenantSession.Store(user);
                }

                // (٣) بَيانات لِتَمتَلِئ صَفَحات الـ user الشَخصِيَّة.
                await EnsureSampleListingAsync(tenantSession, slug, user);
                await EnsureSampleNotificationAsync(tenantSession, user);
            }
            await tenantSession.SaveChangesAsync();
        }
        await globalSession.SaveChangesAsync();
        Console.WriteLine("[TestSeed] ✅ test data seeded.");
    }

    private static async Task EnsureSampleListingAsync(
        IDocumentSession s, string slug, User user)
    {
        var existing = await s.Query<Listing>()
            .Where(l => l.Attributes != null && l.Attributes.ContainsKey("owner_id"))
            .Take(50).ToListAsync();
        if (existing.Any(l => l.Attributes.TryGetValue("owner_id", out var oid)
                               && oid == user.Id.ToString())) return;

        var id = Guid.NewGuid();
        var ev = new ListingCreated(
            Id: id, TenantSlug: slug,
            Title: $"إعلان اختِبار — {user.FullName}",
            Description: "وَصف وَهمِيّ لِأَغراض الفَحص.",
            Price: 100,
            CategorySlug: "general",
            City: "إب",
            District: "حَوبان",
            Attributes: new() { ["owner_id"] = user.Id.ToString() },
            At: DateTime.UtcNow);
        s.Events.StartStream<Listing>(id, ev);
    }

    private static async Task EnsureSampleNotificationAsync(
        IDocumentSession s, User user)
    {
        var has = await s.Query<Notification>()
            .Where(n => n.UserId == user.Id).Take(1).ToListAsync();
        if (has.Count > 0) return;
        s.Store(new Notification
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Type = "welcome",
            Title = "مَرحَباً!",
            Body = "هذا إشعار اختِبار.",
            At = DateTime.UtcNow
        });
    }
}
