using System.Text.Json;
using Ashare.Api.Entities;
using ACommerce.SharedKernel.Abstractions.DynamicAttributes;
using ACommerce.SharedKernel.Abstractions.Repositories;
using Serilog;

namespace Ashare.Api.Services;

/// <summary>
/// بذر البيانات الأولية لعشير. يحاول جلب العروض من الخدمة الخلفية الإنتاجية
/// (api.ashare.sa) وتحويلها إلى الصيغة الجديدة. إن فشل — يعود لبيانات البذر المحلية.
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

    private const string ProductionApiBase = "https://api.ashare.sa";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly IRepositoryFactory _repoFactory;

    public AshareSeeder(IRepositoryFactory repoFactory) => _repoFactory = repoFactory;

    public async Task SeedAsync(CancellationToken ct = default)
    {
        Log.Information(">>> AshareSeeder.SeedAsync starting");
        await SeedCategoriesAsync(ct);
        await SeedUsersAsync(ct);
        await SeedListingsFromProductionAsync(ct);
        await SeedPlansAsync(ct);
        await SeedDefaultSubscriptionsAsync(ct);
        Log.Information(">>> AshareSeeder.SeedAsync finished");
    }

    // ─── Listings: production API first, fallback to local seed ───

    private async Task SeedListingsFromProductionAsync(CancellationToken ct)
    {
        var repo = _repoFactory.CreateRepository<Listing>();
        var existingCount = await repo.CountAsync(cancellationToken: ct);
        Log.Information(">>> SeedListings: existing listings = {Count}", existingCount);

        // Always try production API — even if seed data exists, replace with real data
        Log.Information(">>> SeedListings: calling FetchFromProductionAsync...");
        try
        {
            var (listings, owners) = await FetchFromProductionAsync(ct);
            Log.Information(">>> SeedListings: fetched {L} listings + {O} owners", listings.Count, owners.Count);
            if (listings.Count > 0)
            {
                var userRepo = _repoFactory.CreateRepository<User>();
                foreach (var owner in owners)
                {
                    if (await userRepo.GetByIdAsync(owner.Id, ct) == null)
                        await userRepo.AddAsync(owner, ct);
                }

                foreach (var listing in listings)
                {
                    if (await repo.GetByIdAsync(listing.Id, ct) == null)
                        await repo.AddAsync(listing, ct);
                }

                Log.Information(">>> SeedListings: saved production listings (new={New}, total={Total})",
                    listings.Count, existingCount + listings.Count);
                return;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, ">>> SeedListings: production fetch FAILED — falling back to local");
        }

        if (existingCount > 0) return;
        Log.Information(">>> SeedListings: using local seed data");
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

        // 2) Build local maps: slug → (categoryId, template) AND id → (categoryId, template)
        // Production uses same category IDs as local (10000000-0000-0000-0001-...), so
        // direct ID lookup is the happy path; slug mapping is the fallback.
        var catRepo = _repoFactory.CreateRepository<Category>();
        var localCats = await catRepo.ListAllAsync(ct);
        var slugToLocal = localCats.ToDictionary(
            c => c.Slug,
            c => (c.Id, Template: DynamicAttributeHelper.ParseTemplate(c.AttributeTemplateJson)));
        var idToLocal = localCats.ToDictionary(
            c => c.Id,
            c => (c.Id, Template: DynamicAttributeHelper.ParseTemplate(c.AttributeTemplateJson)));

        // 3) Fetch all listing pages
        var apiListings = new List<JsonElement>();
        const int pageSize = 100;
        var page = 1;
        while (true)
        {
            var url = $"/api/listings?page={page}&pageSize={pageSize}";
            var json = await http.GetStringAsync(url, ct);
            if (page == 1)
                Log.Information("GET {Url} → {Len} bytes. Preview: {Preview}",
                    url, json.Length, json.Length > 500 ? json[..500] : json);

            var doc = JsonDocument.Parse(json);
            var items = ExtractItems(doc.RootElement);
            if (items.Count == 0) break;
            apiListings.AddRange(items);

            var hasNext = TryGetBool(doc.RootElement, "data", "hasNextPage");
            if (!hasNext && items.Count < pageSize) break;
            page++;
        }

        // 4) Map to new entities
        var ownerIds = new HashSet<Guid>();
        var listings = new List<Listing>();
        var defaultCatId = localCats.FirstOrDefault()?.Id ?? CategoryIds.Residential;

        foreach (var el in apiListings)
        {
            var listing = MapListing(el, remoteCatMap, slugToLocal, idToLocal, defaultCatId);
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
        // Plain array response: [{...}, {...}]
        if (root.ValueKind == JsonValueKind.Array)
            return root.EnumerateArray().ToList();

        if (root.ValueKind != JsonValueKind.Object)
            return new();

        // OperationEnvelope<PagedResult>: { data: { items: [...] } }
        if (root.TryGetProperty("data", out var data))
        {
            if (data.ValueKind == JsonValueKind.Array)
                return data.EnumerateArray().ToList();

            if (data.ValueKind == JsonValueKind.Object && data.TryGetProperty("items", out var items)
                && items.ValueKind == JsonValueKind.Array)
                return items.EnumerateArray().ToList();
        }

        // { items: [...] }
        if (root.TryGetProperty("items", out var topItems) && topItems.ValueKind == JsonValueKind.Array)
            return topItems.EnumerateArray().ToList();

        return new();
    }

    private static bool TryGetBool(JsonElement root, string prop1, string prop2)
    {
        if (root.ValueKind != JsonValueKind.Object) return false;
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
        Dictionary<Guid, (Guid Id, AttributeTemplate? Template)> idToLocal,
        Guid defaultCatId)
    {
        var id = GetGuid(el, "id") ?? Guid.NewGuid();
        var ownerId = GetGuid(el, "ownerId") ?? GetGuid(el, "vendorId") ?? Guid.Empty;
        var remoteCatId = GetGuid(el, "categoryId");

        // Map category: first try direct ID (production uses same IDs),
        // then fall back to slug mapping.
        var localCatId = defaultCatId;
        AttributeTemplate? template = null;
        if (remoteCatId.HasValue && idToLocal.TryGetValue(remoteCatId.Value, out var direct))
        {
            localCatId = direct.Id;
            template = direct.Template;
        }
        else if (remoteCatId.HasValue && remoteCatMap.TryGetValue(remoteCatId.Value, out var slug))
        {
            var normalized = slug.Replace('_', '-').ToLowerInvariant();
            if (slugToLocal.TryGetValue(normalized, out var local))
            {
                localCatId = local.Id;
                template = local.Template;
            }
        }

        // Images: prefer native array `images: [...]`, then imagesCsv, then featuredImage
        var imagesCsv = ExtractImagesCsv(el);

        // Attributes: prefer native object `attributes: {...}`, then attributesJson string
        var (attrsDict, dynJson) = ExtractAttributes(el, template);

        // Entity-level fields that may live inside attributes object in production
        var isPhoneAllowed = GetBoolFlex(el, "isPhoneAllowed")
            ?? GetBoolFlex(attrsDict, "is_phone_allowed") ?? true;
        var isWhatsApp = GetBoolFlex(el, "isWhatsAppAllowed") ?? GetBoolFlex(el, "isWhatsappAllowed")
            ?? GetBoolFlex(attrsDict, "is_whatsapp_allowed") ?? true;
        var isMessaging = GetBoolFlex(el, "isMessagingAllowed")
            ?? GetBoolFlex(attrsDict, "is_messaging_allowed") ?? true;
        var licenseNumber = GetString(el, "licenseNumber")
            ?? GetStringFlex(attrsDict, "license_number");
        var duration = GetInt(el, "duration") ?? GetIntFlex(attrsDict, "duration") ?? 1;
        var timeUnit = GetString(el, "timeUnit") ?? GetStringFlex(attrsDict, "time_unit") ?? "month";

        var isActive = GetBool(el, "isActive") ?? true;
        // Status can be a string in production ("Active", "Draft", ...) or int
        var status = ParseStatus(el, isActive);

        return new Listing
        {
            Id = id,
            CreatedAt = GetDateTime(el, "createdAt") ?? DateTime.UtcNow,
            UpdatedAt = GetDateTime(el, "updatedAt"),
            OwnerId = ownerId,
            CategoryId = localCatId,
            Title = GetString(el, "title") ?? GetStringFlex(attrsDict, "title") ?? "",
            Description = GetString(el, "description") ?? GetStringFlex(attrsDict, "description") ?? "",
            Price = GetDecimal(el, "price") ?? 0,
            Duration = duration,
            TimeUnit = timeUnit,
            Currency = GetString(el, "currency") ?? "SAR",
            City = GetString(el, "city") ?? GetStringFlex(attrsDict, "city") ?? "",
            Latitude = GetDouble(el, "latitude"),
            Longitude = GetDouble(el, "longitude"),
            Address = GetString(el, "address"),
            IsPhoneAllowed = isPhoneAllowed,
            IsWhatsAppAllowed = isWhatsApp,
            IsMessagingAllowed = isMessaging,
            LicenseNumber = licenseNumber,
            ImagesCsv = imagesCsv,
            DynamicAttributesJson = dynJson,
            Status = (ListingStatus)status,
            PublishedAt = GetDateTime(el, "publishedAt") ?? (isActive ? GetDateTime(el, "createdAt") : null),
            ViewCount = GetInt(el, "viewCount") ?? 0,
            IsFeatured = GetBool(el, "isFeatured") ?? false,
        };
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

    // ─── Extractors that handle production API shape ───

    private static string? ExtractImagesCsv(JsonElement el)
    {
        // Preferred: native array `images: [url1, url2, ...]`
        if (el.TryGetProperty("images", out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            var urls = arr.EnumerateArray()
                .Where(x => x.ValueKind == JsonValueKind.String)
                .Select(x => x.GetString())
                .Where(s => !string.IsNullOrWhiteSpace(s));
            var csv = string.Join(",", urls);
            if (!string.IsNullOrEmpty(csv)) return csv;
        }
        // Back-compat: imagesCsv, then imagesJson (string), then featuredImage
        var direct = GetString(el, "imagesCsv");
        if (!string.IsNullOrEmpty(direct)) return direct;
        var jsonStr = GetString(el, "imagesJson");
        var fromJson = ParseImagesJsonToCsv(jsonStr);
        if (!string.IsNullOrEmpty(fromJson)) return fromJson;
        return GetString(el, "featuredImage");
    }

    private static (Dictionary<string, object?> Attrs, string? DynJson) ExtractAttributes(
        JsonElement el, AttributeTemplate? template)
    {
        // Preferred: native object `attributes: {...}`
        Dictionary<string, object?>? attrs = null;
        if (el.TryGetProperty("attributes", out var obj) && obj.ValueKind == JsonValueKind.Object)
        {
            attrs = new();
            foreach (var prop in obj.EnumerateObject())
                attrs[prop.Name] = ConvertJsonValue(prop.Value);
        }
        else
        {
            // Back-compat: attributesJson (string) or dynamicAttributesJson passthrough
            var dynPass = GetString(el, "dynamicAttributesJson");
            if (!string.IsNullOrWhiteSpace(dynPass)) return (new(), dynPass);

            var rawAttrs = GetString(el, "attributesJson");
            if (!string.IsNullOrWhiteSpace(rawAttrs))
            {
                try
                {
                    var doc = JsonDocument.Parse(rawAttrs);
                    if (doc.RootElement.ValueKind == JsonValueKind.Object)
                    {
                        attrs = new();
                        foreach (var p in doc.RootElement.EnumerateObject())
                            attrs[p.Name] = ConvertJsonValue(p.Value);
                    }
                }
                catch { }
            }
        }

        if (attrs == null) return (new(), null);
        var dyn = BuildSnapshotJson(attrs, template);
        return (attrs, dyn);
    }

    private static string? BuildSnapshotJson(Dictionary<string, object?> attrs, AttributeTemplate? template)
    {
        if (template == null)
        {
            var snapshot = attrs
                .Where(kv => kv.Value != null)
                .Select((kv, i) => new DynamicAttribute
                {
                    Key = kv.Key, Label = kv.Key, LabelAr = kv.Key,
                    Type = InferType(kv.Value!), Value = kv.Value,
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
                    Type = InferType(value), Value = value,
                    DisplayValue = value.ToString(), DisplayValueAr = value.ToString(),
                    SortOrder = sortCounter++, ShowInCard = false,
                });
        }

        var result = DynamicAttributeHelper.BuildSnapshot(template, templateValues).ToList();
        result.AddRange(extraAttrs);
        return DynamicAttributeHelper.SerializeAttributes(result);
    }

    private static string InferType(object v) => v switch
    {
        bool => "bool",
        int or long => "number",
        double or float or decimal => "decimal",
        System.Collections.IEnumerable and not string => "multi",
        _ => "text"
    };

    private static int ParseStatus(JsonElement el, bool isActive)
    {
        if (el.TryGetProperty("status", out var p))
        {
            if (p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var n)) return n;
            if (p.ValueKind == JsonValueKind.String)
            {
                return p.GetString()?.ToLowerInvariant() switch
                {
                    "active" or "published" => 1,
                    "draft" => 0,
                    "reserved" => 2,
                    "closed" or "inactive" or "archived" or "outofstock" => 3,
                    "rejected" => 4,
                    _ => isActive ? 1 : 0
                };
            }
        }
        return isActive ? 1 : 0;
    }

    // ─── Flexible dict lookups — values may be bool, string, number ───

    private static string? GetStringFlex(Dictionary<string, object?> d, string key)
        => d.TryGetValue(key, out var v) && v != null ? v.ToString() : null;

    private static int? GetIntFlex(Dictionary<string, object?> d, string key)
    {
        if (!d.TryGetValue(key, out var v) || v == null) return null;
        return v switch
        {
            int i => i,
            long l => (int)l,
            double dd => (int)dd,
            _ => int.TryParse(v.ToString(), out var r) ? r : (int?)null
        };
    }

    private static bool? GetBoolFlex(Dictionary<string, object?> d, string key)
    {
        if (!d.TryGetValue(key, out var v) || v == null) return null;
        if (v is bool b) return b;
        return bool.TryParse(v.ToString(), out var r) ? r : (bool?)null;
    }

    private static bool? GetBoolFlex(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var p)) return null;
        if (p.ValueKind is JsonValueKind.True or JsonValueKind.False) return p.GetBoolean();
        if (p.ValueKind == JsonValueKind.String)
            return bool.TryParse(p.GetString(), out var r) ? r : (bool?)null;
        return null;
    }

    // ─── Original JsonElement extractors ───

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
