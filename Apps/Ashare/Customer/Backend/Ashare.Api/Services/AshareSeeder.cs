using System.Text.Json;
using Ashare.Api.Entities;
using ACommerce.SharedKernel.Abstractions.DynamicAttributes;
using ACommerce.SharedKernel.Abstractions.Repositories;
using Serilog;

namespace Ashare.Api.Services;

/// <summary>
/// بذر البيانات الأولية لعشير. يحاول جلب العروض من الخدمة الخلفية الإنتاجية
/// (api.ashare.com) وتحويلها إلى الصيغة الجديدة. إن فشل — يعود لبيانات البذر المحلية.
/// </summary>
public class AshareSeeder
{
    public static class CategoryIds
    {
        public static readonly Guid Residential       = Guid.Parse("10000000-0000-0000-0001-000000000001");
        public static readonly Guid LookingForHousing = Guid.Parse("10000000-0000-0000-0001-000000000002");
        public static readonly Guid LookingForPartner = Guid.Parse("10000000-0000-0000-0001-000000000003");
        public static readonly Guid Administrative    = Guid.Parse("10000000-0000-0000-0001-000000000004");
        public static readonly Guid Commercial        = Guid.Parse("10000000-0000-0000-0001-000000000005");
    }

    public static class UserIds
    {
        public static readonly Guid OwnerAhmed   = Guid.Parse("00000000-0000-0000-0001-000000000001");
        public static readonly Guid CustomerSara = Guid.Parse("00000000-0000-0000-0001-000000000002");
        public static readonly Guid AdminUser    = Guid.Parse("00000000-0000-0000-0001-000000000003");
    }

    private const string ProductionApiBase = "https://api.ashare.com";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly IRepositoryFactory _repoFactory;

    public AshareSeeder(IRepositoryFactory repoFactory) => _repoFactory = repoFactory;

    public async Task SeedAsync(CancellationToken ct = default)
    {
        await SeedCategoriesAsync(ct);
        await SeedUsersAsync(ct);
        await SeedListingsFromProductionAsync(ct);
        await SeedPlansAsync(ct);
        await SeedDefaultSubscriptionsAsync(ct);
    }

    // ─── Listings: production API first, fallback to local seed ───

    private async Task SeedListingsFromProductionAsync(CancellationToken ct)
    {
        var repo = _repoFactory.CreateRepository<Listing>();
        if (await repo.CountAsync(cancellationToken: ct) > 0) return;

        try
        {
            var (listings, owners) = await FetchFromProductionAsync(ct);
            if (listings.Count > 0)
            {
                var userRepo = _repoFactory.CreateRepository<User>();
                await userRepo.AddRangeAsync(owners, ct);
                await repo.AddRangeAsync(listings, ct);
                Log.Information("Seeded {Count} listings + {Owners} owners from production API",
                    listings.Count, owners.Count);
                return;
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Production API fetch failed — falling back to local seed data");
        }

        var now = DateTime.UtcNow;
        await repo.AddRangeAsync(AshareListingsSeed.All(now, UserIds.OwnerAhmed), ct);
    }

    private async Task<(List<Listing>, List<User>)> FetchFromProductionAsync(CancellationToken ct)
    {
        using var http = new HttpClient
        {
            BaseAddress = new Uri(ProductionApiBase),
            Timeout = TimeSpan.FromSeconds(15)
        };

        // 1) Fetch remote categories → build remoteId → slug map
        var remoteCatMap = await FetchRemoteCategoryMapAsync(http, ct);

        // 2) Build local slug → (categoryId, template) map
        var catRepo = _repoFactory.CreateRepository<Category>();
        var localCats = await catRepo.ListAllAsync(ct);
        var slugToLocal = localCats.ToDictionary(
            c => c.Slug,
            c => (c.Id, Template: DynamicAttributeHelper.ParseTemplate(c.AttributeTemplateJson)));

        // 3) Fetch all listing pages
        var apiListings = new List<JsonElement>();
        var page = 1;
        while (true)
        {
            var json = await http.GetStringAsync($"/api/listings?page={page}&pageSize=100", ct);
            var doc = JsonDocument.Parse(json);

            // Handle OperationEnvelope<PagedResult<T>>: data.items
            // or plain array, or data as array
            var items = ExtractItems(doc.RootElement);
            if (items.Count == 0) break;
            apiListings.AddRange(items);

            var hasNext = TryGetBool(doc.RootElement, "data", "hasNextPage");
            if (!hasNext) break;
            page++;
        }

        // 4) Map to new entities
        var ownerIds = new HashSet<Guid>();
        var listings = new List<Listing>();
        var defaultCatId = localCats.FirstOrDefault()?.Id ?? CategoryIds.Residential;

        foreach (var el in apiListings)
        {
            var listing = MapListing(el, remoteCatMap, slugToLocal, defaultCatId);
            listings.Add(listing);
            ownerIds.Add(listing.OwnerId);
        }

        // 5) Create placeholder users for owners not already seeded
        var existingUserIds = new[] { UserIds.OwnerAhmed, UserIds.CustomerSara, UserIds.AdminUser }.ToHashSet();
        var now = DateTime.UtcNow;
        var newOwners = ownerIds
            .Where(id => !existingUserIds.Contains(id))
            .Select(id => new User
            {
                Id = id,
                CreatedAt = now,
                PhoneNumber = id.ToString(),
                FullName = "Production Owner",
                IsActive = true,
                Role = "owner"
            })
            .ToList();

        Log.Information("Fetched {Count} listings from {Url}, {Owners} new owners",
            listings.Count, ProductionApiBase, newOwners.Count);
        return (listings, newOwners);
    }

    // ─── API parsing helpers ───

    private static async Task<Dictionary<Guid, string>> FetchRemoteCategoryMapAsync(
        HttpClient http, CancellationToken ct)
    {
        var map = new Dictionary<Guid, string>();
        try
        {
            var json = await http.GetStringAsync("/api/categories", ct);
            var doc = JsonDocument.Parse(json);
            var items = ExtractItems(doc.RootElement);
            foreach (var el in items)
            {
                if (el.TryGetProperty("id", out var idProp) && Guid.TryParse(idProp.GetString(), out var id)
                    && el.TryGetProperty("slug", out var slugProp))
                    map[id] = slugProp.GetString() ?? "";
            }
        }
        catch { /* category mapping optional — will use default */ }
        return map;
    }

    private static List<JsonElement> ExtractItems(JsonElement root)
    {
        // Try: { data: { items: [...] } }  (OperationEnvelope<PagedResult>)
        if (root.TryGetProperty("data", out var data))
        {
            if (data.ValueKind == JsonValueKind.Object && data.TryGetProperty("items", out var items)
                && items.ValueKind == JsonValueKind.Array)
                return items.EnumerateArray().ToList();

            // Try: { data: [...] }  (OperationEnvelope<List>)
            if (data.ValueKind == JsonValueKind.Array)
                return data.EnumerateArray().ToList();
        }

        // Try: plain array
        if (root.ValueKind == JsonValueKind.Array)
            return root.EnumerateArray().ToList();

        // Try: { items: [...] }
        if (root.TryGetProperty("items", out var topItems) && topItems.ValueKind == JsonValueKind.Array)
            return topItems.EnumerateArray().ToList();

        return new();
    }

    private static bool TryGetBool(JsonElement root, string prop1, string prop2)
    {
        if (root.TryGetProperty(prop1, out var p1) && p1.TryGetProperty(prop2, out var p2)
            && p2.ValueKind is JsonValueKind.True or JsonValueKind.False)
            return p2.GetBoolean();
        return false;
    }

    // ─── Mapping: API JSON → Listing entity ───

    private static Listing MapListing(
        JsonElement el,
        Dictionary<Guid, string> remoteCatMap,
        Dictionary<string, (Guid Id, AttributeTemplate? Template)> slugToLocal,
        Guid defaultCatId)
    {
        var id = GetGuid(el, "id") ?? Guid.NewGuid();
        var ownerId = GetGuid(el, "ownerId") ?? GetGuid(el, "vendorId") ?? Guid.Empty;
        var remoteCatId = GetGuid(el, "categoryId");

        // Map remote categoryId → slug → local categoryId + template
        var localCatId = defaultCatId;
        AttributeTemplate? template = null;
        if (remoteCatId.HasValue && remoteCatMap.TryGetValue(remoteCatId.Value, out var slug))
        {
            var normalized = slug.Replace('_', '-').ToLowerInvariant();
            if (slugToLocal.TryGetValue(normalized, out var local))
            {
                localCatId = local.Id;
                template = local.Template;
            }
        }

        // Images: imagesJson (array) → CSV, or imagesCsv directly
        var imagesCsv = GetString(el, "imagesCsv");
        if (string.IsNullOrEmpty(imagesCsv))
        {
            var imagesJson = GetString(el, "imagesJson");
            imagesCsv = ParseImagesJsonToCsv(imagesJson);
        }
        if (string.IsNullOrEmpty(imagesCsv))
            imagesCsv = GetString(el, "featuredImage");

        // Dynamic attributes: dynamicAttributesJson directly, or convert from attributesJson
        var dynJson = GetString(el, "dynamicAttributesJson");
        if (string.IsNullOrEmpty(dynJson))
        {
            var rawAttrs = GetString(el, "attributesJson");
            dynJson = ConvertAttributesToSnapshot(rawAttrs, template);
        }

        var isActive = GetBool(el, "isActive") ?? true;
        var status = GetInt(el, "status") ?? (isActive ? 1 : 0);

        return new Listing
        {
            Id = id,
            CreatedAt = GetDateTime(el, "createdAt") ?? DateTime.UtcNow,
            UpdatedAt = GetDateTime(el, "updatedAt"),
            OwnerId = ownerId,
            CategoryId = localCatId,
            Title = GetString(el, "title") ?? "",
            Description = GetString(el, "description") ?? "",
            Price = GetDecimal(el, "price") ?? 0,
            Duration = GetInt(el, "duration") ?? 1,
            TimeUnit = GetString(el, "timeUnit") ?? "month",
            Currency = GetString(el, "currency") ?? "SAR",
            City = GetString(el, "city") ?? "",
            Latitude = GetDouble(el, "latitude"),
            Longitude = GetDouble(el, "longitude"),
            Address = GetString(el, "address"),
            IsPhoneAllowed = GetBool(el, "isPhoneAllowed") ?? true,
            IsWhatsAppAllowed = GetBool(el, "isWhatsAppAllowed") ?? GetBool(el, "isWhatsappAllowed") ?? true,
            IsMessagingAllowed = GetBool(el, "isMessagingAllowed") ?? true,
            LicenseNumber = GetString(el, "licenseNumber"),
            ImagesCsv = imagesCsv,
            DynamicAttributesJson = dynJson,
            Status = (ListingStatus)status,
            PublishedAt = GetDateTime(el, "publishedAt") ?? (isActive ? GetDateTime(el, "createdAt") : null),
            ViewCount = GetInt(el, "viewCount") ?? 0,
            IsFeatured = GetBool(el, "isFeatured") ?? false,
        };
    }

    private static string? ConvertAttributesToSnapshot(string? rawJson, AttributeTemplate? template)
    {
        if (string.IsNullOrWhiteSpace(rawJson)) return null;

        Dictionary<string, object?> attrs;
        try
        {
            var doc = JsonDocument.Parse(rawJson);
            attrs = new();
            foreach (var p in doc.RootElement.EnumerateObject())
                attrs[p.Name] = ConvertJsonValue(p.Value);
        }
        catch { return null; }

        if (template == null)
        {
            var snapshot = attrs
                .Where(kv => kv.Value != null)
                .Select((kv, i) => new DynamicAttribute
                {
                    Key = kv.Key, Label = kv.Key, LabelAr = kv.Key,
                    Type = "text", Value = kv.Value,
                    DisplayValue = kv.Value?.ToString(), DisplayValueAr = kv.Value?.ToString(),
                    SortOrder = i, ShowInCard = false,
                }).ToList();
            return DynamicAttributeHelper.SerializeAttributes(snapshot);
        }

        var templateKeys = template.Fields.Select(f => f.Key).ToHashSet();
        var templateValues = new Dictionary<string, object?>();
        var extraAttrs = new List<DynamicAttribute>();
        var sortCounter = 1000;

        foreach (var (key, value) in attrs)
        {
            if (templateKeys.Contains(key))
                templateValues[key] = value;
            else if (value != null)
                extraAttrs.Add(new DynamicAttribute
                {
                    Key = key, Label = key, LabelAr = key,
                    Type = "text", Value = value,
                    DisplayValue = value.ToString(), DisplayValueAr = value.ToString(),
                    SortOrder = sortCounter++, ShowInCard = false,
                });
        }

        var result = DynamicAttributeHelper.BuildSnapshot(template, templateValues).ToList();
        result.AddRange(extraAttrs);
        return DynamicAttributeHelper.SerializeAttributes(result);
    }

    private static object? ConvertJsonValue(JsonElement el) => el.ValueKind switch
    {
        JsonValueKind.String => el.GetString(),
        JsonValueKind.Number => el.TryGetInt64(out var l) ? l : el.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Array => el.EnumerateArray().Select(ConvertJsonValue).ToList(),
        _ => null
    };

    private static string? ParseImagesJsonToCsv(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var urls = JsonSerializer.Deserialize<List<string>>(json, JsonOpts);
            return urls is { Count: > 0 } ? string.Join(",", urls) : null;
        }
        catch { return null; }
    }

    // ─── JsonElement field extractors ───

    private static string? GetString(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var p)) return null;
        return p.ValueKind == JsonValueKind.String ? p.GetString() : null;
    }
    private static Guid? GetGuid(JsonElement el, string name)
    {
        var s = GetString(el, name);
        return Guid.TryParse(s, out var g) ? g : null;
    }
    private static int? GetInt(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var p)) return null;
        return p.ValueKind == JsonValueKind.Number ? p.GetInt32() : null;
    }
    private static decimal? GetDecimal(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var p)) return null;
        return p.ValueKind == JsonValueKind.Number ? p.GetDecimal() : null;
    }
    private static double? GetDouble(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var p)) return null;
        return p.ValueKind == JsonValueKind.Number ? p.GetDouble() : null;
    }
    private static bool? GetBool(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var p)) return null;
        return p.ValueKind is JsonValueKind.True or JsonValueKind.False ? p.GetBoolean() : null;
    }
    private static DateTime? GetDateTime(JsonElement el, string name)
    {
        var s = GetString(el, name);
        return DateTime.TryParse(s, out var dt) ? DateTime.SpecifyKind(dt, DateTimeKind.Utc) : null;
    }

    // ─── Other seed methods (unchanged) ───

    private async Task SeedPlansAsync(CancellationToken ct)
    {
        var repo = _repoFactory.CreateRepository<Plan>();
        if (await repo.CountAsync(cancellationToken: ct) > 0) return;
        foreach (var plan in AsharePlansSeed.GetAll())
            await repo.AddAsync(plan, ct);
    }

    private async Task SeedDefaultSubscriptionsAsync(CancellationToken ct)
    {
        var subRepo = _repoFactory.CreateRepository<Subscription>();
        if (await subRepo.CountAsync(cancellationToken: ct) > 0) return;
        var now = DateTime.UtcNow;
        await subRepo.AddAsync(new Subscription
        {
            Id = Guid.NewGuid(), CreatedAt = now,
            UserId = UserIds.OwnerAhmed,
            PlanId = AsharePlansSeed.PlanIds.BusinessAnnual,
            BillingCycle = "annual", StartDate = now, EndDate = now.AddYears(1),
            Status = SubscriptionStatus.Active, AmountPaid = 4800, Currency = "SAR"
        }, ct);
    }

    private async Task SeedCategoriesAsync(CancellationToken ct)
    {
        var repo = _repoFactory.CreateRepository<Category>();
        if (await repo.CountAsync(cancellationToken: ct) > 0) return;

        var categories = new List<Category>
        {
            Cat(CategoryIds.Residential,       "residential",          "سكني",            "Residential",         "home",      1, AshareCategoryTemplates.Residential()),
            Cat(CategoryIds.LookingForHousing,  "looking-for-housing", "طلب سكن",         "Looking for Housing", "search",    2, AshareCategoryTemplates.LookingForHousing()),
            Cat(CategoryIds.LookingForPartner,  "looking-for-partner", "طلب شريك سكن",    "Looking for Roommate","users",     3, AshareCategoryTemplates.LookingForPartner()),
            Cat(CategoryIds.Administrative,     "administrative",      "مساحة إدارية",    "Administrative",      "briefcase", 4, AshareCategoryTemplates.Administrative()),
            Cat(CategoryIds.Commercial,         "commercial",          "مساحة تجارية",    "Commercial",          "store",     5, AshareCategoryTemplates.Commercial()),
        };

        foreach (var c in categories) await repo.AddAsync(c, ct);
    }

    private static Category Cat(Guid id, string slug, string ar, string en, string icon, int sort, AttributeTemplate tpl)
        => new()
        {
            Id = id, Slug = slug, NameAr = ar, NameEn = en, Icon = icon,
            SortOrder = sort, CreatedAt = DateTime.UtcNow,
            AttributeTemplateJson = DynamicAttributeHelper.SerializeTemplate(tpl)
        };

    private async Task SeedUsersAsync(CancellationToken ct)
    {
        var repo = _repoFactory.CreateRepository<User>();
        if (await repo.CountAsync(cancellationToken: ct) > 0) return;
        var now = DateTime.UtcNow;
        await repo.AddRangeAsync(new[]
        {
            new User { Id = UserIds.OwnerAhmed,   CreatedAt = now, PhoneNumber = "+966500000001", Email = "ahmed@ashare.test", FullName = "أحمد المالك",   NationalId = "1000000001", NafathVerified = true, IsActive = true, Role = "owner"    },
            new User { Id = UserIds.CustomerSara,  CreatedAt = now, PhoneNumber = "+966500000002", Email = "sara@ashare.test",  FullName = "سارة العميلة",  NationalId = "2000000002", NafathVerified = true, IsActive = true, Role = "customer" },
            new User { Id = UserIds.AdminUser,     CreatedAt = now, PhoneNumber = "+966500000003", Email = "admin@ashare.test", FullName = "المسؤول",       IsActive = true, Role = "admin" },
        }, ct);
    }
}
