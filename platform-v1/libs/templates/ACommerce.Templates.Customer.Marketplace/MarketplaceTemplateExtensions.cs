using ACommerce.Kit.Auth;
using ACommerce.Kit.Auth.Server;
using ACommerce.Kit.Chat;
using ACommerce.Kit.Favorites;
using ACommerce.Kit.Listings;
using ACommerce.Platform.Shared;
using ACommerce.Templates.Customer.Marketplace.Gates;
using Marten;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.Templates.Customer.Marketplace;

/// <summary>
/// نُقطَة دُخول واحِدَة للتَطبيق: <c>services.AddCustomerMarketplaceTemplate()</c>
/// + <c>app.MapCustomerMarketplaceTemplate()</c>. يَجمَع AuthSession +
/// كلّ form endpoints (auth/logout/chat send/listing-chat-start) في
/// مَكان واحِد. التَطبيق لا يَكتُب أيّ منها.
/// </summary>
public static class MarketplaceTemplateExtensions
{
    public static IServiceCollection AddCustomerMarketplaceTemplate(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<AuthSession>();
        services.AddScoped<L>();
        services.AddScoped<ACommerce.Kit.Realtime.Client.RealtimeClient>();
        services.AddScoped<ACommerce.Templates.Customer.Marketplace.Services.DynamicAttributesService>();
        services.AddSingleton<ACommerce.Templates.Customer.Marketplace.Services.IAgentBackend>(
            sp => ACommerce.Templates.Customer.Marketplace.Services.AgentBackendFactory
                .Create(sp.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>()));
        services.AddSingleton<ACommerce.Templates.Customer.Marketplace.Services.AgentService>();
        services.AddSingleton<ACommerce.Templates.Customer.Marketplace.Services.AgentToolExecutor>();
        services.AddSingleton<ACommerce.Templates.Customer.Marketplace.Services.WebPushService>();
        services.AddScoped<Gates.GatePipeline>();
        services.AddScoped<Commands.AcceptTermsHandler>();

        // ─── طبقة التحليل الاستثماري (الحاضنة) ──────────────────────────
        services.AddSingleton<Services.Incubator.SaudiDataProvider>();
        services.AddSingleton<Services.Incubator.FeasibilityPromptBuilder>();
        services.AddScoped<Services.Incubator.FeasibilityAnalysisService>();
        services.AddScoped<Services.Incubator.StudioAuth>();
        return services;
    }

    public static IEndpointRouteBuilder MapCustomerMarketplaceTemplate(this IEndpointRouteBuilder app)
    {
        // ─── Phone OTP ──────────────────────────────────────────────────
        app.MapPost("/{slug}/auth/phone/login",
            async (string slug, HttpRequest req, IDocumentStore store,
                   ITenantContext tenant, IOtpChannel channel) =>
        {
            if (!tenant.IsResolved) return Results.NotFound();
            var phone = req.Form["phone"].ToString().Trim();
            var asRole = req.Form["as"].ToString().Trim();
            if (string.IsNullOrEmpty(phone))
                return Results.Redirect(Link(req, slug,
                    $"login?err=phone_required" +
                    (string.IsNullOrEmpty(asRole) ? "" : $"&as={Uri.EscapeDataString(asRole)}")));
            await AuthHandlers.RequestPhoneOtpHandler(new RequestPhoneOtp(phone), tenant, channel, default);
            var asParam = string.IsNullOrEmpty(asRole) ? "" : $"&as={Uri.EscapeDataString(asRole)}";
            return Results.Redirect(Link(req, slug, $"login?stage=verify&phone={Uri.EscapeDataString(phone)}{asParam}"));
        }).DisableAntiforgery();

        app.MapPost("/{slug}/auth/phone/verify",
            async (string slug, HttpRequest req, HttpResponse res, IDocumentStore store, ITenantContext tenant) =>
        {
            if (!tenant.IsResolved) return Results.NotFound();
            var phone = req.Form["phone"].ToString().Trim();
            var code = req.Form["code"].ToString().Trim();
            var result = await AuthHandlers.VerifyPhoneOtpHandler(new VerifyPhoneOtp(phone, code), tenant, store);
            if (result is null)
                return Results.Redirect(Link(req, slug,
                    $"login?stage=verify&phone={Uri.EscapeDataString(phone)}&err=code_invalid"));
            var asRole = req.Form["as"].ToString().Trim().ToLowerInvariant();
            // كَتابَة cookie باسم يَتَضَمَّن الدَور — يَسمَح بِجَلَسات مُتَوازِيَة
            // (راكِب في تَبويب، سائِق في آخَر) في نَفس المُتَصَفِّح.
            AuthSession.WriteCookie(res, slug, result,
                role: string.IsNullOrEmpty(asRole) ? null : asRole);
            if (!string.IsNullOrEmpty(asRole))
                await AssignRoleAsync(slug, result.UserId, asRole, store);
            // إن كانَ المُستَخدِم أُنشِئ تَوّاً، أَخطِر مُديري المَتجَر.
            await using (var qs = store.QuerySession(slug))
            {
                var user = await qs.LoadAsync<User>(result.UserId);
                if (user is not null && (DateTime.UtcNow - user.CreatedAt).TotalMinutes < 1)
                    await NotifyAdminsAsync(store, slug, "new_user",
                        "مُستَخدِم جَديد سَجَّل",
                        $"{user.FullName} · {user.Phone}",
                        $"/admin/tenants/{slug}/users");
            }
            return Results.Redirect(await PostLoginRouteAsync(slug, result.UserId, asRole, store));
        }).DisableAntiforgery();

        // ─── Nafath ─────────────────────────────────────────────────────
        app.MapPost("/{slug}/auth/nafath/login",
            async (string slug, HttpRequest req, ITenantContext tenant, INafathChannel channel) =>
        {
            if (!tenant.IsResolved) return Results.NotFound();
            var nid = req.Form["nid"].ToString().Trim();
            if (string.IsNullOrEmpty(nid) || nid.Length != 10)
                return Results.Redirect(Link(req, slug, $"login?err=nid_required"));
            var pending = await AuthHandlers.RequestNafathHandler(new RequestNafath(nid), tenant, channel, default);
            return Results.Redirect(Link(req, slug,
                $"login?stage=verify&nid={Uri.EscapeDataString(nid)}" +
                $"&attempt={pending.AttemptId}&code={pending.DisplayCode}"));
        }).DisableAntiforgery();

        app.MapPost("/{slug}/auth/nafath/verify",
            async (string slug, HttpRequest req, HttpResponse res,
                   ITenantContext tenant, INafathChannel channel, IDocumentStore store) =>
        {
            if (!tenant.IsResolved) return Results.NotFound();
            var nid = req.Form["nid"].ToString().Trim();
            var attempt = req.Form["attempt"].ToString();
            var result = await AuthHandlers.VerifyNafathHandler(
                new VerifyNafath(attempt, nid), tenant, channel, store, default);
            if (result is null)
                return Results.Redirect(Link(req, slug,
                    $"login?stage=verify&nid={Uri.EscapeDataString(nid)}" +
                    $"&attempt={attempt}&code=00&err=not_approved"));
            var asRole = req.Form["as"].ToString().Trim().ToLowerInvariant();
            AuthSession.WriteCookie(res, slug, result,
                role: string.IsNullOrEmpty(asRole) ? null : asRole);
            if (!string.IsNullOrEmpty(asRole))
                await AssignRoleAsync(slug, result.UserId, asRole, store);
            return Results.Redirect(await PostLoginRouteAsync(slug, result.UserId, asRole, store));
        }).DisableAntiforgery();

        // ─── Language toggle ─────────────────────────────────────────────
        app.MapPost("/lang/{lang}", (string lang, HttpRequest req, HttpResponse res) =>
        {
            var l = lang == "en" ? "en" : "ar";
            res.Cookies.Append(L.CookieName, l, new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                IsEssential = true, Path = "/", SameSite = SameSiteMode.Lax
            });
            var ret = req.Form["return"].ToString();
            return Results.Redirect(string.IsNullOrEmpty(ret) ? "/" : ret);
        }).DisableAntiforgery();

        // ─── Logout ─────────────────────────────────────────────────────
        app.MapPost("/{slug}/auth/logout", (string slug, HttpContext http) =>
        {
            AuthSession.ClearCookie(http.Response, slug);
            return Results.Redirect($"/{slug}");
        }).DisableAntiforgery();

        // ─── Favorite toggle ────────────────────────────────────────────
        app.MapPost("/{slug}/listings/{id:guid}/favorite",
            async (string slug, Guid id, HttpRequest req, IDocumentStore store) =>
        {
            var token = req.Cookies[AuthSession.CookieName(slug)];
            var parsed = AuthHandlers.ParseToken(token);
            if (parsed is null) return Results.Redirect(Link(req, slug, $"login"));
            var (userId, _, _) = parsed.Value;

            await using var s = store.LightweightSession(slug);
            var favId = Favorite.MakeId(userId, id);
            var existing = await s.LoadAsync<Favorite>(favId);
            if (existing is null)
            {
                s.Store(new Favorite { Id = favId, UserId = userId, ListingId = id });
            }
            else
            {
                s.Delete(existing);
            }
            await s.SaveChangesAsync();
            var ret = req.Form["return"].ToString();
            return Results.Redirect(string.IsNullOrEmpty(ret) ? $"/{slug}/listings/{id}" : ret);
        }).DisableAntiforgery();

        // ─── Start chat from listing ────────────────────────────────────
        app.MapPost("/{slug}/listings/{id:guid}/chat",
            async (string slug, Guid id, HttpRequest req, IDocumentStore store) =>
        {
            var token = req.Cookies[AuthSession.CookieName(slug)];
            var parsed = AuthHandlers.ParseToken(token);
            if (parsed is null) return Results.Redirect(Link(req, slug, $"login?returnUrl=/{slug}/listings/{id}"));
            var (userId, tenantSlug, _) = parsed.Value;
            if (tenantSlug != slug) return Results.Redirect(Link(req, slug, $"login"));
            var userName = req.Cookies[AuthSession.CookieName(slug) + ".name"] ?? "أنا";

            await using var s = store.LightweightSession(slug);
            var listing = await s.Events.AggregateStreamAsync<Listing>(id);
            if (listing is null) return Results.Redirect($"/{slug}");

            var existing = await s.Query<Conversation>()
                .Where(c => c.ListingId == id && (c.OwnerId == userId || c.PartnerId == userId))
                .FirstOrDefaultAsync();
            Guid convId;
            if (existing is not null) convId = existing.Id;
            else
            {
                var conv = new Conversation
                {
                    Id = Guid.NewGuid(),
                    OwnerId = userId, OwnerName = userName,
                    PartnerId = Guid.NewGuid(), PartnerName = "صاحِب الإعلان",
                    Subject = listing.Title, ListingId = id, LastAt = DateTime.UtcNow
                };
                s.Store(conv);
                await s.SaveChangesAsync();
                convId = conv.Id;
            }
            return Results.Redirect(Link(req, slug, $"chats/{convId}"));
        }).DisableAntiforgery();

        // ─── Pick role (after first login or via switch) ────────────────
        app.MapPost("/{slug}/me/role/save",
            async (string slug, HttpRequest req, IDocumentStore store) =>
        {
            var token = req.Cookies[AuthSession.CookieName(slug)];
            var parsed = AuthHandlers.ParseToken(token);
            if (parsed is null) return Results.Redirect(Link(req, slug, $"login"));
            var (userId, _, _) = parsed.Value;

            var role = req.Form["role"].ToString().Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(role))
                return Results.Redirect(Link(req, slug, $"me/role"));

            await using var sg = store.QuerySession();
            var tenant = await sg.LoadAsync<ACommerce.Kit.Tenants.Tenant>(slug);
            if (tenant is null) return Results.Redirect("/admin");
            var picked = tenant.Roles.FirstOrDefault(r => r.Slug == role);
            if (picked is null) return Results.Redirect(Link(req, slug, $"me/role?err=invalid_role"));
            // أَدوار إداريَّة لا يُمكِن مَنحُها ذاتيّاً — يُجَهَّز التَّعيين
            // مِن قِبَل إداريّ آخَر أَو DB seed.
            if (picked.CatalogSlug == "tenant_admin")
                return Results.Redirect(Link(req, slug, $"me/role?err=admin_self_grant"));

            await using var s = store.LightweightSession(slug);
            var user = await s.LoadAsync<User>(userId);
            if (user is null) return Results.Redirect(Link(req, slug, $"me"));
            user.ActiveRole = role;
            user.UpdatedAt = DateTime.UtcNow;
            s.Store(user);
            await s.SaveChangesAsync();

            // إن كانَ لِلدَور حُقول بَيانات مَطلوبَة غَير مَملوءَة، حَوِّل
            // إلى onboarding. لَو البَيانات مَوجودَة (مَثَلاً المُستَخدِم
            // عَبَّأَها سابِقاً ثُمّ بَدَّلَ الدَور)، اِذهَب مُباشَرَة إلى
            // HomeRoute بِلا إعادَة طَلَب.
            var roleValues = user.RoleAttributesJson.TryGetValue(picked.Slug, out var rv)
                ? rv : new Dictionary<string, string>();
            var needsOnboarding = picked.Fields
                .Where(f => f.IsRequired)
                .Any(f => !roleValues.TryGetValue(f.Code, out var v) || string.IsNullOrEmpty(v));
            if (needsOnboarding)
                return Results.Redirect(Link(req, slug, $"me/role/onboarding"));
            return Results.Redirect(string.IsNullOrEmpty(picked.HomeRoute)
                ? $"/{slug}" : $"/{slug}{picked.HomeRoute}");
        }).DisableAntiforgery();

        app.MapPost("/{slug}/me/role/onboarding/save",
            async (string slug, HttpRequest req, IDocumentStore store) =>
        {
            var token = req.Cookies[AuthSession.CookieName(slug)];
            var parsed = AuthHandlers.ParseToken(token);
            if (parsed is null) return Results.Redirect(Link(req, slug, $"login"));
            var (userId, _, _) = parsed.Value;

            await using var sg = store.QuerySession();
            var tenant = await sg.LoadAsync<ACommerce.Kit.Tenants.Tenant>(slug);
            if (tenant is null) return Results.Redirect($"/{slug}");

            await using var s = store.LightweightSession(slug);
            var user = await s.LoadAsync<User>(userId);
            if (user is null) return Results.Redirect(Link(req, slug, $"me"));

            foreach (var (key, vals) in req.Form)
            {
                if (!key.StartsWith("role_", StringComparison.Ordinal)) continue;
                var rest = key["role_".Length..];
                var attrIdx = rest.IndexOf("_attr_", StringComparison.Ordinal);
                if (attrIdx <= 0) continue;
                var roleSlug = rest[..attrIdx];
                var attrCode = rest[(attrIdx + "_attr_".Length)..];
                if (!user.RoleAttributesJson.TryGetValue(roleSlug, out var dict))
                {
                    dict = new Dictionary<string, string>();
                    user.RoleAttributesJson[roleSlug] = dict;
                }
                dict[attrCode] = vals.ToString();
            }
            user.UpdatedAt = DateTime.UtcNow;
            s.Store(user);
            await s.SaveChangesAsync();

            var active = tenant.Roles.FirstOrDefault(r => r.Slug == user.ActiveRole);
            return Results.Redirect(string.IsNullOrEmpty(active?.HomeRoute)
                ? $"/{slug}" : $"/{slug}{active.HomeRoute}");
        }).DisableAntiforgery();

        // ─── Profile save ───────────────────────────────────────────────
        app.MapPost("/{slug}/me/save",
            async (string slug, HttpRequest req, IDocumentStore store) =>
        {
            var token = req.Cookies[AuthSession.CookieName(slug)];
            var parsed = AuthHandlers.ParseToken(token);
            if (parsed is null) return Results.Redirect(Link(req, slug, $"login"));
            var (userId, _, _) = parsed.Value;
            var fullName = req.Form["fullName"].ToString().Trim();
            if (fullName.Length == 0) return Results.Redirect(Link(req, slug, $"me/edit"));

            // الخَصائِص الديناميكِيَّة: كُلّ حَقل بِالـ form بِالبادِئَة
            // attr_<Code> يُحَدِّث user.AttributesJson. لا نَمسَح المَفاتيح
            // غَير المَوجودَة (سَلوك upsert: نُحَدِّث المُمَرَّر، نَتُرك الباقي).
            await using var s = store.LightweightSession(slug);
            var user = await s.LoadAsync<User>(userId);
            if (user is null) return Results.Redirect(Link(req, slug, $"me"));
            user.FullName = fullName;
            user.UpdatedAt = DateTime.UtcNow;

            // الدَور النَّشِط (يَظهَر فَقَط لَو المَتجَر يُعَرِّف أَدواراً).
            var activeRole = req.Form["activeRole"].ToString().Trim();
            if (!string.IsNullOrEmpty(activeRole)) user.ActiveRole = activeRole;

            // خَصائِص ديناميكِيَّة: attr_<Code> = بروفايل عامّ،
            // role_<roleSlug>_attr_<Code> = خاصّ بِدَور.
            foreach (var (key, vals) in req.Form)
            {
                if (key.StartsWith("attr_", StringComparison.Ordinal))
                {
                    user.AttributesJson[key["attr_".Length..]] = vals.ToString();
                }
                else if (key.StartsWith("role_", StringComparison.Ordinal))
                {
                    // role_{slug}_attr_{code}
                    var rest = key["role_".Length..];
                    var attrIdx = rest.IndexOf("_attr_", StringComparison.Ordinal);
                    if (attrIdx <= 0) continue;
                    var roleSlug = rest[..attrIdx];
                    var attrCode = rest[(attrIdx + "_attr_".Length)..];
                    if (string.IsNullOrEmpty(roleSlug) || string.IsNullOrEmpty(attrCode)) continue;
                    if (!user.RoleAttributesJson.TryGetValue(roleSlug, out var dict))
                    {
                        dict = new Dictionary<string, string>();
                        user.RoleAttributesJson[roleSlug] = dict;
                    }
                    dict[attrCode] = vals.ToString();
                }
            }
            s.Store(user);
            await s.SaveChangesAsync();

            AuthSession.UpdateNameCookie(req.HttpContext.Response, slug, fullName);
            return Results.Redirect(Link(req, slug, $"me"));
        }).DisableAntiforgery();

        // ─── Plans subscribe ────────────────────────────────────────────
        app.MapPost("/{slug}/plans/{planId}/subscribe",
            async (string slug, string planId, HttpRequest req, IDocumentStore store) =>
        {
            var token = req.Cookies[AuthSession.CookieName(slug)];
            var parsed = AuthHandlers.ParseToken(token);
            if (parsed is null) return Results.Redirect(Link(req, slug, $"login?returnUrl=/{slug}/plans"));
            var (userId, _, _) = parsed.Value;

            await using var s = store.LightweightSession(slug);
            var plan = await s.LoadAsync<ACommerce.Kit.Subscriptions.Plan>(planId);
            if (plan is null) return Results.Redirect(Link(req, slug, $"plans"));
            var ev = new ACommerce.Kit.Subscriptions.SubscriptionCreated(
                Guid.NewGuid(), userId, planId, plan.ListingsQuota, plan.DaysPeriod, DateTime.UtcNow);
            s.Events.StartStream<ACommerce.Kit.Subscriptions.Subscription>(ev.Id, ev);
            await s.SaveChangesAsync();
            return Results.Redirect(Link(req, slug, $"me"));
        }).DisableAntiforgery();

        // ─── Support open ticket ────────────────────────────────────────
        app.MapPost("/{slug}/support/open",
            async (string slug, HttpRequest req, IDocumentStore store) =>
        {
            var token = req.Cookies[AuthSession.CookieName(slug)];
            var parsed = AuthHandlers.ParseToken(token);
            if (parsed is null) return Results.Redirect(Link(req, slug, $"login"));
            var (userId, _, _) = parsed.Value;
            var userName = req.Cookies[AuthSession.CookieName(slug) + ".name"] ?? "—";
            var subject = req.Form["subject"].ToString().Trim();
            var body    = req.Form["body"].ToString().Trim();
            if (subject.Length == 0 || body.Length == 0) return Results.Redirect(Link(req, slug, $"support"));

            await using var s = store.LightweightSession(slug);
            var ev = new ACommerce.Kit.Support.TicketCreated(
                Guid.NewGuid(), userId, userName, subject, body, DateTime.UtcNow);
            s.Events.StartStream<ACommerce.Kit.Support.Ticket>(ev.Id, ev);
            await s.SaveChangesAsync();
            return Results.Redirect(Link(req, slug, $"support"));
        }).DisableAntiforgery();

        // ─── Report listing — يَفتَح طَلَب دَعم مُسبَق التَعبِئَة ─────────
        app.MapPost("/{slug}/listings/{id:guid}/report",
            async (string slug, Guid id, HttpRequest req, IDocumentStore store) =>
        {
            var token = req.Cookies[AuthSession.CookieName(slug)];
            var parsed = AuthHandlers.ParseToken(token);
            if (parsed is null) return Results.Redirect(Link(req, slug, $"login?returnUrl=/{slug}/listings/{id}"));
            var (userId, tenantSlug, _) = parsed.Value;
            if (tenantSlug != slug) return Results.Redirect(Link(req, slug, $"login"));
            var userName = req.Cookies[AuthSession.CookieName(slug) + ".name"] ?? "—";

            var reason = req.Form["reason"].ToString().Trim();
            var note   = req.Form["note"].ToString().Trim();
            if (string.IsNullOrEmpty(reason)) reason = "غَير مُحَدَّد";

            await using var s = store.LightweightSession(slug);
            var ev = new ACommerce.Kit.Support.TicketCreated(
                Guid.NewGuid(), userId, userName,
                Subject: $"تَبليغ: {reason}",
                Body:    $"الإعلان: /{slug}/listings/{id}\nالسَّبَب: {reason}\n\n{note}",
                At:      DateTime.UtcNow);
            s.Events.StartStream<ACommerce.Kit.Support.Ticket>(ev.Id, ev);
            await s.SaveChangesAsync();
            await NotifyAdminsAsync(store, slug, "report",
                $"بَلاغ: {reason}",
                $"{userName} بَلَّغَ عَن إعلان",
                $"/admin/tenants/{slug}/tickets");
            return Results.Redirect(Link(req, slug, $"listings/{id}?reported=1"));
        }).DisableAntiforgery();

        // ─── Create listing — gates: auth + terms + permission ──────────
        // الـ filters تَتَكَفَّل بِالتَّوثيق وَالشُروط وَ "listing.create".
        app.MapPost("/{slug}/listings/create",
            async (string slug, HttpContext http, HttpRequest req, IDocumentStore store,
                   Microsoft.AspNetCore.SignalR.IHubContext<ACommerce.Kit.Realtime.Server.RealtimeHub> hub,
                   ACommerce.Templates.Customer.Marketplace.Services.WebPushService push) =>
        {
            var userId = http.UserId();

            var title       = req.Form["title"].ToString().Trim();
            var description = req.Form["description"].ToString().Trim();
            var category    = req.Form["category"].ToString().Trim();
            var city        = req.Form["city"].ToString().Trim();
            var district    = req.Form["district"].ToString().Trim();
            var priceStr    = req.Form["price"].ToString().Trim();
            var acceptsOffers = req.Form["attr_accepts_offers"].ToString()
                .Equals("true", StringComparison.OrdinalIgnoreCase);

            // الحَدّ الأَدنَى لِلسِعر = ١، إلّا لَو الإعلان طَلَب مَفتوح لِلعُروض
            // (الراكِب يَترُك السِعر صِفراً، السائِق يُحَدِّدُه في عَرضِه).
            decimal.TryParse(priceStr, out var price);
            var priceOk = acceptsOffers ? price >= 0 : price > 0;
            if (title.Length < 3 || string.IsNullOrEmpty(category) || !priceOk)
            {
                return Results.Redirect(Link(req, slug, $"create-listing?err=invalid"));
            }

            // الخَصائِص الديناميكِيَّة: كُلّ حَقل بِالـ form بِالبادِئَة
            // attr_<Code> يَدخُل في Listing.Attributes.
            var dynAttrs = req.Form
                .Where(kv => kv.Key.StartsWith("attr_", StringComparison.Ordinal))
                .ToDictionary(
                    kv => kv.Key["attr_".Length..],
                    kv => kv.Value.ToString());
            // اِحفَظ مالِك الإعلان كَخاصِّيَّة لِأَنّ Listing event مازال
            // بِلا OwnerId مُهَيكَل. صَفحَة /me/listings تَستَعمِلها لِلفَلتَرَة.
            dynAttrs["owner_id"] = userId.ToString();

            await using var s = store.LightweightSession(slug);
            var id = Guid.NewGuid();
            var ev = new ListingCreated(
                id, slug, title,
                string.IsNullOrEmpty(description) ? null : description,
                price, category,
                string.IsNullOrEmpty(city) ? null : city,
                string.IsNullOrEmpty(district) ? null : district,
                dynAttrs,
                DateTime.UtcNow);
            s.Events.StartStream<Listing>(id, ev);

            // مُطابَقَة البَحوث المَحفوظَة — لِكُلّ SavedSearch مَفعَّل
            // يَنطَبِق عَلى هذا الإعلان، أَنشِئ Notification لِصاحِبه.
            // المُطابَقَة في الذاكِرَة (مِئات الـ searches لِلتَّينَنت كَحَدّ
            // أَعلى مَعقول).
            var newListing = new Listing
            {
                Id = id, TenantSlug = slug, Title = title, Description = description,
                Price = price, CategorySlug = category, City = city, District = district,
                Attributes = new(dynAttrs), CreatedAt = ev.At
            };
            var savedSearches = await s.Query<ACommerce.Kit.SavedSearches.SavedSearch>()
                .Where(ss => ss.IsEnabled).ToListAsync();
            var nudged = new HashSet<Guid>();
            foreach (var ss in savedSearches)
            {
                if (!ss.Matches(newListing)) continue;
                s.Store(new ACommerce.Kit.Notifications.Notification
                {
                    Id = Guid.NewGuid(),
                    UserId = ss.UserId,
                    Type = "saved_search_match",
                    Title = $"إعلان جَديد يُطابِق «{ss.Label}»",
                    Body = title,
                    RelatedUrl = $"/{slug}/listings/{id}",
                    At = DateTime.UtcNow
                });
                nudged.Add(ss.UserId);
            }

            await s.SaveChangesAsync();
            foreach (var uid in nudged)
            {
                await NudgeAsync(hub, slug, uid);
                await push.SendAsync(store, slug, uid,
                    "إعلان جَديد يُطابِق بَحثكَ",
                    title,
                    url: $"/{slug}/listings/{id}",
                    tag: $"ss-{id}");
            }
            await NotifyAdminsAsync(store, slug, "new_listing",
                "إعلان جَديد",
                title,
                $"/{slug}/listings/{id}", hub);
            return Results.Redirect(Link(req, slug, $"listings/{id}"));
        }).DisableAntiforgery().RequireAuth().RequireTerms().RequirePermission("listing.create");

        // ─── Saved Searches — create/delete/toggle ──────────────────────
        app.MapPost("/{slug}/searches/save",
            async (string slug, HttpRequest req, IDocumentStore store) =>
        {
            var token = req.Cookies[AuthSession.CookieName(slug)];
            var parsed = AuthHandlers.ParseToken(token);
            if (parsed is null) return Results.Redirect(Link(req, slug, $"login"));
            var (userId, _, _) = parsed.Value;

            var label = req.Form["label"].ToString().Trim();
            if (string.IsNullOrEmpty(label)) label = "بَحث جَديد";

            var ss = new ACommerce.Kit.SavedSearches.SavedSearch
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Label = label,
                CategorySlug = NullIfEmpty(req.Form["category"].ToString()),
                City         = NullIfEmpty(req.Form["city"].ToString()),
                District     = NullIfEmpty(req.Form["district"].ToString())
            };
            if (decimal.TryParse(req.Form["min"].ToString(), out var min) && min > 0) ss.MinPrice = min;
            if (decimal.TryParse(req.Form["max"].ToString(), out var max) && max > 0) ss.MaxPrice = max;

            foreach (var (key, vals) in req.Form)
            {
                if (!key.StartsWith("attr_", StringComparison.Ordinal)) continue;
                var v = vals.ToString();
                if (!string.IsNullOrEmpty(v)) ss.Criteria[key["attr_".Length..]] = v;
            }

            await using var s = store.LightweightSession(slug);
            s.Store(ss);
            await s.SaveChangesAsync();
            return Results.Redirect(Link(req, slug, $"me/searches?saved=1"));

            static string? NullIfEmpty(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        }).DisableAntiforgery();

        app.MapPost("/{slug}/searches/{id:guid}/delete",
            async (string slug, Guid id, HttpRequest req, IDocumentStore store) =>
        {
            var token = req.Cookies[AuthSession.CookieName(slug)];
            var parsed = AuthHandlers.ParseToken(token);
            if (parsed is null) return Results.Redirect(Link(req, slug, $"login"));
            var (userId, _, _) = parsed.Value;

            await using var s = store.LightweightSession(slug);
            var ss = await s.LoadAsync<ACommerce.Kit.SavedSearches.SavedSearch>(id);
            if (ss is null || ss.UserId != userId)
                return Results.Redirect(Link(req, slug, $"me/searches"));
            s.Delete(ss);
            await s.SaveChangesAsync();
            return Results.Redirect(Link(req, slug, $"me/searches"));
        }).DisableAntiforgery();

        app.MapPost("/{slug}/searches/{id:guid}/toggle",
            async (string slug, Guid id, HttpRequest req, IDocumentStore store) =>
        {
            var token = req.Cookies[AuthSession.CookieName(slug)];
            var parsed = AuthHandlers.ParseToken(token);
            if (parsed is null) return Results.Redirect(Link(req, slug, $"login"));
            var (userId, _, _) = parsed.Value;

            await using var s = store.LightweightSession(slug);
            var ss = await s.LoadAsync<ACommerce.Kit.SavedSearches.SavedSearch>(id);
            if (ss is null || ss.UserId != userId)
                return Results.Redirect(Link(req, slug, $"me/searches"));
            ss.IsEnabled = !ss.IsEnabled;
            s.Store(ss);
            await s.SaveChangesAsync();
            return Results.Redirect(Link(req, slug, $"me/searches"));
        }).DisableAntiforgery();

        // ─── Submit offer on a listing ──────────────────────────────────
        app.MapPost("/{slug}/listings/{id:guid}/offers",
            async (string slug, Guid id, HttpRequest req, IDocumentStore store) =>
        {
            var token = req.Cookies[AuthSession.CookieName(slug)];
            var parsed = AuthHandlers.ParseToken(token);
            if (parsed is null) return Results.Redirect(Link(req, slug, $"login?returnUrl=/{slug}/listings/{id}"));
            var (userId, tenantSlug, _) = parsed.Value;
            if (tenantSlug != slug) return Results.Redirect(Link(req, slug, $"login"));

            if (!await HasPermissionAsync(slug, userId, "offer.submit", store))
                return Results.Redirect(Link(req, slug, $"listings/{id}?err=forbidden"));
            var userName = req.Cookies[AuthSession.CookieName(slug) + ".name"] ?? "—";

            var priceStr = req.Form["price"].ToString().Trim();
            var message  = req.Form["message"].ToString().Trim();
            var latStr   = req.Form["lat"].ToString().Trim();
            var lngStr   = req.Form["lng"].ToString().Trim();
            var ttlStr   = req.Form["ttl_minutes"].ToString().Trim();

            // فَلتَرَة صارِمَة: سِعر مَوجَب فَقَط، وَ مَوقِع غَير-صِفر مَطلوب.
            if (!decimal.TryParse(priceStr, out var price) || price <= 0)
                return Results.Redirect(Link(req, slug, $"listings/{id}?err=offer_price"));
            _ = double.TryParse(latStr, System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture, out var lat);
            _ = double.TryParse(lngStr, System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture, out var lng);
            if (lat == 0 && lng == 0)
                return Results.Redirect(Link(req, slug, $"listings/{id}?err=offer_geo"));
            _ = int.TryParse(ttlStr, out var ttl);
            if (ttl <= 0) ttl = 15;

            await using var s = store.LightweightSession(slug);
            var listing = await s.Events.AggregateStreamAsync<Listing>(id);
            if (listing is null) return Results.Redirect($"/{slug}");
            // مَنع صاحِب الإعلان مِن تَقديم عَرض عَلى نَفسه.
            if (listing.Attributes.TryGetValue("owner_id", out var ownerStr2) &&
                ownerStr2 == userId.ToString())
                return Results.Redirect(Link(req, slug, $"listings/{id}?err=self_offer"));

            // مَنع تَقديم عَرض جَديد إن كانَ السائِق في رِحلَة نَشِطَة، أَو
            // إن قَطَع رِحلَة في آخِر ٥ دَقائِق (تَهدِئَة لِمَنع الاستِغلال).
            var matches = await s.Query<ACommerce.Kit.Offers.ListingMatch>().ToListAsync();
            var active = matches.FirstOrDefault(m =>
                m.OffererId == userId &&
                m.Status == ACommerce.Kit.Offers.TripStatus.Active);
            if (active is not null)
                return Results.Redirect(Link(req, slug, $"listings/{active.Id}?err=active_trip"));
            var lastAbort = matches
                .Where(m => m.OffererId == userId &&
                            m.Status == ACommerce.Kit.Offers.TripStatus.Aborted &&
                            m.ResolvedBy == "offerer" &&
                            m.ResolvedAt.HasValue)
                .OrderByDescending(m => m.ResolvedAt).FirstOrDefault();
            if (lastAbort is not null &&
                (DateTime.UtcNow - lastAbort.ResolvedAt!.Value).TotalMinutes < 5)
                return Results.Redirect(Link(req, slug, $"listings/{id}?err=cooldown"));

            // اِجمَع خَصائِص العَرض الديناميكِيَّة مِن أَيّ حَقل بِالبادِئَة
            // attr_ (مَثَلاً attr_seats=4 أَو attr_eta_minutes=8).
            var offerAttrs = req.Form
                .Where(kv => kv.Key.StartsWith("attr_", StringComparison.Ordinal))
                .ToDictionary(kv => kv.Key["attr_".Length..], kv => kv.Value.ToString());

            var oid = Guid.NewGuid();
            var ev = new ACommerce.Kit.Offers.OfferSubmitted(
                oid, id, userId, userName, price,
                string.IsNullOrEmpty(message) ? null : message,
                lat, lng,
                DateTime.UtcNow.AddMinutes(ttl), DateTime.UtcNow,
                offerAttrs.Count > 0 ? offerAttrs : null);
            s.Events.StartStream<ACommerce.Kit.Offers.Offer>(oid, ev);
            await s.SaveChangesAsync();
            return Results.Redirect(Link(req, slug, $"listings/{id}?offer=submitted"));
        }).DisableAntiforgery();

        // ─── Accept an offer (listing owner) ────────────────────────────
        app.MapPost("/{slug}/offers/{id:guid}/accept",
            async (string slug, Guid id, HttpRequest req, IDocumentStore store,
                   Microsoft.AspNetCore.SignalR.IHubContext<ACommerce.Kit.Realtime.Server.RealtimeHub> hub,
                   ACommerce.Templates.Customer.Marketplace.Services.WebPushService push) =>
        {
            var token = req.Cookies[AuthSession.CookieName(slug)];
            var parsed = AuthHandlers.ParseToken(token);
            if (parsed is null) return Results.Redirect(Link(req, slug, $"login"));
            var (acceptorId, tenantSlug, _) = parsed.Value;
            if (tenantSlug != slug) return Results.Redirect(Link(req, slug, $"login"));

            await using var s = store.LightweightSession(slug);
            var offer = await s.Events.AggregateStreamAsync<ACommerce.Kit.Offers.Offer>(id);
            if (offer is null || offer.Status != ACommerce.Kit.Offers.OfferStatus.Pending)
                return Results.Redirect(Link(req, slug, $"listings/{(offer?.ListingId ?? Guid.Empty)}"));

            // مالِك الإعلان فَقَط يَقبَل العَرض — التَّحَقُّق عَبر خاصِّيَّة
            // owner_id المَحفوظَة عِندَ الإنشاء. الـ UI أَيضاً يُخفي زِرّ
            // القَبول عَن غَير المالِك لكِنّ التَّحَقُّق هُنا هو الحاجِز
            // الفِعليّ.
            var listing = await s.Events.AggregateStreamAsync<Listing>(offer.ListingId);
            if (listing is null) return Results.Redirect($"/{slug}");
            if (!listing.Attributes.TryGetValue("owner_id", out var ownerStr) ||
                ownerStr != acceptorId.ToString())
                return Results.Redirect(Link(req, slug, $"listings/{offer.ListingId}?err=not_owner"));

            var now = DateTime.UtcNow;
            s.Events.Append(id, new ACommerce.Kit.Offers.OfferAccepted(id, now));

            // اِكتُب ListingMatch لِيَعرِف الواجِهَة أَنّ الإعلان مُتَطابِق.
            s.Store(new ACommerce.Kit.Offers.ListingMatch
            {
                Id = offer.ListingId,
                AcceptedOfferId = id,
                OffererId = offer.OffererId,
                OffererName = offer.OffererName,
                AcceptedPrice = offer.Price,
                OffererLat = offer.Lat,
                OffererLng = offer.Lng,
                MatchedAt = now
            });

            // أَغلِق العُروض الأُخرى عَلى نَفس الإعلان تِلقائيّاً.
            var siblings = await s.Query<ACommerce.Kit.Offers.Offer>()
                .Where(o => o.ListingId == offer.ListingId
                         && o.Id != id
                         && o.Status == ACommerce.Kit.Offers.OfferStatus.Pending)
                .ToListAsync();
            foreach (var sib in siblings)
                s.Events.Append(sib.Id, new ACommerce.Kit.Offers.OfferRejected(sib.Id, now));

            // افتَح مُحادَثَة مُؤَقَّتَة لِلتَنسيق — تَنتَهي بَعد 24 ساعَة.
            // الـ Owner هُنا = المُتَّصِل (مالِك الإعلان)، Partner = مُقَدِّم العَرض.
            var acceptorName = req.Cookies[AuthSession.CookieName(slug) + ".name"] ?? "أنا";
            var conv = new Conversation
            {
                Id = Guid.NewGuid(),
                OwnerId = acceptorId, OwnerName = acceptorName,
                PartnerId = offer.OffererId, PartnerName = offer.OffererName,
                Subject = $"تَنسيق عَرض بِـ {offer.Price:N0} ريال",
                ListingId = offer.ListingId,
                LastAt = now,
                ExpiresAt = now.AddHours(24),
                LinkedOfferId = id
            };
            s.Store(conv);

            // إشعار لِلسائِق بِأَنّ عَرضَه قُبِلَ.
            s.Store(new ACommerce.Kit.Notifications.Notification
            {
                Id = Guid.NewGuid(),
                UserId = offer.OffererId,
                Type = "offer_accepted",
                Title = "تَمّ قَبول عَرضكَ ✓",
                Body  = $"{acceptorName} قَبِلَ عَرضكَ بِـ {offer.Price:N0} ريال. افتَح المُحادَثَة لِلتَنسيق.",
                RelatedUrl = $"/{slug}/chats/{conv.Id}",
                At = now
            });

            await s.SaveChangesAsync();
            // أَخطِر السائِق فَوراً — الإشعار + المُحادَثَة ظَهَرا.
            await NudgeAsync(hub, slug, offer.OffererId);
            await push.SendAsync(store, slug, offer.OffererId,
                "تَمّ قَبول عَرضكَ ✓",
                $"{acceptorName} قَبِلَ عَرضكَ بِـ {offer.Price:N0} ريال.",
                url: $"/{slug}/chats/{conv.Id}",
                tag: $"offer-{id}");
            return Results.Redirect(Link(req, slug, $"chats/{conv.Id}"));
        }).DisableAntiforgery();

        // ─── Reject / Withdraw offer ────────────────────────────────────
        app.MapPost("/{slug}/offers/{id:guid}/reject",
            async (string slug, Guid id, HttpRequest req, IDocumentStore store) =>
        {
            var token = req.Cookies[AuthSession.CookieName(slug)];
            var parsed = AuthHandlers.ParseToken(token);
            if (parsed is null) return Results.Redirect(Link(req, slug, $"login"));
            var (rejectorId, _, _) = parsed.Value;

            await using var s = store.LightweightSession(slug);
            var offer = await s.Events.AggregateStreamAsync<ACommerce.Kit.Offers.Offer>(id);
            if (offer is null || offer.Status != ACommerce.Kit.Offers.OfferStatus.Pending)
                return Results.Redirect($"/{slug}");

            // فَقَط مالِك الإعلان يَستَطيع رَفض عَرض (مَن أَرادَ سَحب
            // عَرضِه يَستَخدِم /withdraw).
            var listing = await s.Events.AggregateStreamAsync<Listing>(offer.ListingId);
            if (listing is null ||
                !listing.Attributes.TryGetValue("owner_id", out var ownerStr) ||
                ownerStr != rejectorId.ToString())
                return Results.Redirect(Link(req, slug, $"listings/{offer.ListingId}?err=not_owner"));

            s.Events.Append(id, new ACommerce.Kit.Offers.OfferRejected(id, DateTime.UtcNow));
            await s.SaveChangesAsync();
            return Results.Redirect(Link(req, slug, $"listings/{offer.ListingId}"));
        }).DisableAntiforgery();

        // ─── Trip lifecycle — driver marks "arrived at pickup" ───────────
        // فَحص قُرب: السائِق يُرسِل مَوقِعَه الحاليّ، نُقارِنه مَع
        // pickup_lat/pickup_lng. لَو > 1 كم يُرفَض الادِّعاء.
        app.MapPost("/{slug}/trips/{listingId:guid}/arrived",
            async (string slug, Guid listingId, HttpRequest req, IDocumentStore store,
                   Microsoft.AspNetCore.SignalR.IHubContext<ACommerce.Kit.Realtime.Server.RealtimeHub> hub,
                   ACommerce.Templates.Customer.Marketplace.Services.WebPushService push) =>
        {
            var token = req.Cookies[AuthSession.CookieName(slug)];
            var parsed = AuthHandlers.ParseToken(token);
            if (parsed is null) return Results.Redirect(Link(req, slug, $"login"));
            var (userId, _, _) = parsed.Value;

            _ = double.TryParse(req.Form["lat"].ToString(),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var lat);
            _ = double.TryParse(req.Form["lng"].ToString(),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var lng);
            if (lat == 0 && lng == 0)
                return Results.Redirect(Link(req, slug, $"listings/{listingId}?err=arrived_geo"));

            await using var s = store.LightweightSession(slug);
            var match = await s.LoadAsync<ACommerce.Kit.Offers.ListingMatch>(listingId);
            if (match is null || match.Status != ACommerce.Kit.Offers.TripStatus.Active)
                return Results.Redirect(Link(req, slug, $"listings/{listingId}"));
            if (match.OffererId != userId)
                return Results.Redirect(Link(req, slug, $"listings/{listingId}?err=not_driver"));

            var listing = await s.Events.AggregateStreamAsync<Listing>(listingId);
            if (listing is null) return Results.Redirect($"/{slug}");

            // قارِن المَسافَة بَين مَوقِع السائِق وَ نُقطَة الانطِلاق.
            if (listing.Attributes.TryGetValue("pickup_lat", out var plat) &&
                listing.Attributes.TryGetValue("pickup_lng", out var plng) &&
                double.TryParse(plat, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var pLat) &&
                double.TryParse(plng, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var pLng))
            {
                var dKm = ACommerce.Kit.Offers.OfferHelpers.DistanceKm(lat, lng, pLat, pLng);
                if (dKm > 1.0)   // عَتَبَة 1 كم — يُمكِن جَعلُها قابِلَة لِلتَكوين
                    return Results.Redirect(
                        $"/{slug}/listings/{listingId}?err=too_far&dist={dKm:0.#}");
            }

            match.ArrivedAt = DateTime.UtcNow;
            match.ArrivedLat = lat;
            match.ArrivedLng = lng;
            s.Store(match);

            // أَخطِر الراكِب — السائِق وَصَل.
            s.Store(new ACommerce.Kit.Notifications.Notification
            {
                Id = Guid.NewGuid(),
                UserId = ParseListingOwnerId(listing) ?? Guid.Empty,
                Type = "driver_arrived",
                Title = "السائِق وَصَل ✓",
                Body  = $"{match.OffererName} في نُقطَة الانطِلاق.",
                RelatedUrl = $"/{slug}/listings/{listingId}",
                At = DateTime.UtcNow
            });

            await s.SaveChangesAsync();
            var ownerGuid = ParseListingOwnerId(listing);
            if (ownerGuid.HasValue)
            {
                await NudgeAsync(hub, slug, ownerGuid.Value);
                await push.SendAsync(store, slug, ownerGuid.Value,
                    "السائِق وَصَل ✓",
                    $"{match.OffererName} في نُقطَة الانطِلاق.",
                    url: $"/{slug}/listings/{listingId}",
                    tag: $"arrived-{listingId}");
            }
            return Results.Redirect(Link(req, slug, $"listings/{listingId}?trip=arrived"));
        }).DisableAntiforgery();

        // ─── Trip lifecycle — complete / abort ──────────────────────────
        // كِلاهُما عَلى مُستَوى الإعلان (ListingId)، لِأَنّ ListingMatch
        // doc بِالـ Id = ListingId. مَن يُؤَكِّد: owner (الراكِب) أَو
        // offerer (السائِق المَقبول).
        app.MapPost("/{slug}/trips/{listingId:guid}/complete",
            async (string slug, Guid listingId, HttpRequest req, IDocumentStore store) =>
        {
            var token = req.Cookies[AuthSession.CookieName(slug)];
            var parsed = AuthHandlers.ParseToken(token);
            if (parsed is null) return Results.Redirect(Link(req, slug, $"login"));
            var (userId, _, _) = parsed.Value;

            await using var s = store.LightweightSession(slug);
            var match = await s.LoadAsync<ACommerce.Kit.Offers.ListingMatch>(listingId);
            if (match is null || match.Status != ACommerce.Kit.Offers.TripStatus.Active)
                return Results.Redirect(Link(req, slug, $"listings/{listingId}"));

            var listing = await s.Events.AggregateStreamAsync<Listing>(listingId);
            var isOwner = listing is not null &&
                          listing.Attributes.TryGetValue("owner_id", out var oid) &&
                          oid == userId.ToString();
            var isOfferer = match.OffererId == userId;
            if (!isOwner && !isOfferer)
                return Results.Redirect(Link(req, slug, $"listings/{listingId}?err=not_party"));

            match.Status = ACommerce.Kit.Offers.TripStatus.Completed;
            match.ResolvedAt = DateTime.UtcNow;
            match.ResolvedBy = isOwner ? "owner" : "offerer";
            s.Store(match);

            // أَنهِ المُحادَثَة المُؤَقَّتَة المُرتَبِطَة بِالعَرض المَقبول.
            var conv = (await s.Query<Conversation>()
                .Where(c => c.LinkedOfferId == match.AcceptedOfferId).ToListAsync())
                .FirstOrDefault();
            if (conv is not null)
            {
                conv.ExpiresAt = DateTime.UtcNow;   // = انتَهَت فَوراً
                s.Store(conv);
            }
            await s.SaveChangesAsync();
            return Results.Redirect(Link(req, slug, $"listings/{listingId}?trip=completed"));
        }).DisableAntiforgery();

        app.MapPost("/{slug}/trips/{listingId:guid}/abort",
            async (string slug, Guid listingId, HttpRequest req, IDocumentStore store) =>
        {
            var token = req.Cookies[AuthSession.CookieName(slug)];
            var parsed = AuthHandlers.ParseToken(token);
            if (parsed is null) return Results.Redirect(Link(req, slug, $"login"));
            var (userId, _, _) = parsed.Value;
            var reason = req.Form["reason"].ToString().Trim();

            await using var s = store.LightweightSession(slug);
            var match = await s.LoadAsync<ACommerce.Kit.Offers.ListingMatch>(listingId);
            if (match is null || match.Status != ACommerce.Kit.Offers.TripStatus.Active)
                return Results.Redirect(Link(req, slug, $"listings/{listingId}"));

            var listing = await s.Events.AggregateStreamAsync<Listing>(listingId);
            var isOwner = listing is not null &&
                          listing.Attributes.TryGetValue("owner_id", out var oid) &&
                          oid == userId.ToString();
            var isOfferer = match.OffererId == userId;
            if (!isOwner && !isOfferer)
                return Results.Redirect(Link(req, slug, $"listings/{listingId}?err=not_party"));

            match.Status = ACommerce.Kit.Offers.TripStatus.Aborted;
            match.ResolvedAt = DateTime.UtcNow;
            match.ResolvedBy = isOwner ? "owner" : "offerer";
            match.AbortReason = string.IsNullOrEmpty(reason) ? null : reason;
            s.Store(match);

            var conv = (await s.Query<Conversation>()
                .Where(c => c.LinkedOfferId == match.AcceptedOfferId).ToListAsync())
                .FirstOrDefault();
            if (conv is not null)
            {
                conv.ExpiresAt = DateTime.UtcNow;
                s.Store(conv);
            }
            await s.SaveChangesAsync();
            return Results.Redirect(Link(req, slug, $"listings/{listingId}?trip=aborted"));
        }).DisableAntiforgery();

        app.MapPost("/{slug}/offers/{id:guid}/withdraw",
            async (string slug, Guid id, HttpRequest req, IDocumentStore store) =>
        {
            var token = req.Cookies[AuthSession.CookieName(slug)];
            var parsed = AuthHandlers.ParseToken(token);
            if (parsed is null) return Results.Redirect(Link(req, slug, $"login"));
            var (offererId, _, _) = parsed.Value;

            await using var s = store.LightweightSession(slug);
            var offer = await s.Events.AggregateStreamAsync<ACommerce.Kit.Offers.Offer>(id);
            if (offer is null || offer.Status != ACommerce.Kit.Offers.OfferStatus.Pending)
                return Results.Redirect($"/{slug}");
            // فَقَط مُقَدِّم العَرض يَسحَب عَرضَه.
            if (offer.OffererId != offererId)
                return Results.Redirect(Link(req, slug, $"me/offers"));
            s.Events.Append(id, new ACommerce.Kit.Offers.OfferWithdrawn(id, DateTime.UtcNow));
            await s.SaveChangesAsync();
            return Results.Redirect(Link(req, slug, $"me/offers"));
        }).DisableAntiforgery();

        // ─── PWA — Service Worker على الجَذر ──────────────────────────
        // الـ wwwroot لِلمَكتَبَة يُقَدَّم تَحت /_content/<lib>/، لكِنّ SW
        // scope مَحدود تَحت مَسار المَلَفّ نَفسه. فَلِيَستَطيع تَسجيله بِـ
        // scope /{slug}/ يَجِب تَقديمه مِن جَذر المَوقِع.
        // نَستَخدِم WebRootFileProvider الَّذي يَجمَع static assets كُلّ
        // المَكتَبات؛ ابحَث أَوَّلاً في /sw.js لِلتَطبيق المُستَهلِك (تَجاوُز
        // اختِياريّ)، ثُمَّ في /_content/<this-lib>/sw.js.
        app.MapGet("/sw.js", (HttpResponse res, Microsoft.AspNetCore.Hosting.IWebHostEnvironment env) =>
        {
            var fp = env.WebRootFileProvider;
            var candidates = new[]
            {
                "/sw.js",
                "/_content/ACommerce.Templates.Customer.Marketplace/sw.js"
            };
            foreach (var path in candidates)
            {
                var fi = fp.GetFileInfo(path);
                if (fi.Exists)
                {
                    using var s = fi.CreateReadStream();
                    using var ms = new MemoryStream();
                    s.CopyTo(ms);
                    // Service-Worker-Allowed يُوَسِّع الـ scope المَسموح بِه
                    // فَوق مَسار المَلَفّ — نَسمَح بِالجَذر "/".
                    res.Headers["Service-Worker-Allowed"] = "/";
                    return Results.File(ms.ToArray(), "application/javascript",
                        lastModified: fi.LastModified);
                }
            }
            return Results.NotFound();
        });

        // offline.html عَلى الجَذر أَيضاً (لِيَستَطيع SW الوُصول إلَيها).
        app.MapGet("/offline.html", (Microsoft.AspNetCore.Hosting.IWebHostEnvironment env) =>
        {
            var fp = env.WebRootFileProvider;
            foreach (var path in new[] { "/offline.html",
                "/_content/ACommerce.Templates.Customer.Marketplace/offline.html" })
            {
                var fi = fp.GetFileInfo(path);
                if (fi.Exists)
                {
                    using var s = fi.CreateReadStream();
                    using var ms = new MemoryStream();
                    s.CopyTo(ms);
                    return Results.File(ms.ToArray(), "text/html; charset=utf-8");
                }
            }
            return Results.NotFound();
        });

        // ─── PWA — manifest + icons لِكُلّ تَطبيق فَرعيّ ──────────────────
        // كُلّ (slug, role) لَه manifest مُستَقِلّ بِاسم وَلَون وَأَيقونَة
        // مُلائِمَة. الـ scope يُحدِّد حَدّ الـ PWA — تَنَقُّل المُستَخدِم
        // خارِجَه يَفتَحه المُتَصَفِّح كَ صَفحَة عاديَّة. لِمَتاجِر بِلا
        // أَدوار (ashare/ejar) نَعرِض manifest عَلى /{slug} بِلا role.
        app.MapGet("/api/{slug}/manifest.json", async (
            string slug, IDocumentStore store) =>
            await BuildManifestAsync(slug, role: null, store));

        app.MapGet("/api/{slug}/r/{role}/manifest.json", async (
            string slug, string role, IDocumentStore store) =>
            await BuildManifestAsync(slug, role, store));

        // أَيقونَة تِلقائيَّة SVG — تَستَخدِم لَون المَتجَر + الحَرف الأَوَّل
        // مِن اسم الدَور (أَو إيموجي الدَور إن كانَ مَضبوطاً). إذا كانَ
        // المُصَمِّم رَفَعَ أَيقونَة مُخَصَّصَة (Role.PwaIconUrl) نُحَوِّل لَها.
        app.MapGet("/api/{slug}/icon.svg", async (
            string slug, IDocumentStore store) =>
            await BuildIconAsync(slug, role: null, store));

        app.MapGet("/api/{slug}/r/{role}/icon.svg", async (
            string slug, string role, IDocumentStore store) =>
            await BuildIconAsync(slug, role, store));

        // ─── PWA — VAPID public key (لِـ JS لِبَناء PushSubscription) ─────
        // الـ public key لَيس سِرّاً — يَكفي أَن يَكون مُتاحاً لِأَيّ client.
        // الـ private key يَبقى فَقَط في السيرفر.
        app.MapGet("/api/push/vapid-key",
            (ACommerce.Templates.Customer.Marketplace.Services.WebPushService push)
                => Results.Text(push.PublicKey, "text/plain"));

        // ─── PWA — Web Push subscribe endpoint ───────────────────────────
        // الـ client (sw.js) يَستَلِم رِسالَة Push مِن السيرفر. هذا الـ
        // endpoint يَحفَظ subscription المُستَخدِم لِيَستَطيع السيرفر
        // إرسال push لاحِقاً.
        app.MapPost("/api/{slug}/push/subscribe",
            async (string slug, HttpRequest req, IDocumentStore store) =>
        {
            var token = req.Cookies[AuthSession.CookieName(slug)];
            // جَرِّب رول-سكوبد cookies إذا الـ legacy ما وُجِدَ.
            if (string.IsNullOrEmpty(token))
            {
                var role = AuthSession.ExtractRoleFromPath(req.Path);
                if (role is not null)
                    token = req.Cookies[AuthSession.CookieName(slug, role)];
            }
            var parsed = AuthHandlers.ParseToken(token);
            if (parsed is null) return Results.Unauthorized();
            var (userId, _, _) = parsed.Value;

            using var doc = await System.Text.Json.JsonDocument.ParseAsync(req.Body);
            var root = doc.RootElement;
            var endpoint = root.GetProperty("endpoint").GetString() ?? "";
            var keys = root.GetProperty("keys");
            var p256dh = keys.GetProperty("p256dh").GetString() ?? "";
            var auth   = keys.GetProperty("auth").GetString() ?? "";
            if (string.IsNullOrEmpty(endpoint)) return Results.BadRequest();

            await using var s = store.LightweightSession(slug);
            var user = await s.LoadAsync<User>(userId);
            if (user is null) return Results.NotFound();
            // اِستَبدِل subscription بِنَفس الـ endpoint (نَفس الجِهاز/المُتَصَفِّح)
            user.PushSubscriptions.RemoveAll(p => p.Endpoint == endpoint);
            user.PushSubscriptions.Add(new ACommerce.Kit.Auth.PushSubscription
            {
                Endpoint = endpoint, P256dh = p256dh, Auth = auth,
                CreatedAt = DateTime.UtcNow
            });
            s.Store(user);
            await s.SaveChangesAsync();
            return Results.Ok();
        }).DisableAntiforgery();

        // ─── Terms acceptance — Phase 2 demo: command + pipeline pattern ───
        // الـ adapter يَجمَع المُدخَلات مِن HTTP، يُنشِئ command، يُمَرِّره
        // لِلـ pipeline. الـ pipeline يَفحَص IRequireAuth + IRequireTenant
        // ثُمَّ يَستَدعي الـ handler. لا boilerplate cookie هُنا.
        app.MapPost("/{slug}/terms/accept", async (
            string slug, HttpRequest req, HttpContext http,
            Gates.GatePipeline pipeline, Commands.AcceptTermsHandler handler) =>
        {
            var userId = http.UserId();
            var role   = http.Role();
            var returnUrl = req.Query["returnUrl"].ToString();
            if (string.IsNullOrEmpty(returnUrl) || !returnUrl.StartsWith("/"))
                returnUrl = AuthSession.LinkFor(slug, role, "");

            var cmd = new Commands.AcceptTermsCommand(userId, slug, TermsPolicy.CurrentVersion);
            try
            {
                await pipeline.ExecuteAsync(cmd, () => handler.HandleAsync(cmd));
            }
            catch (Gates.GateDeniedException ex)
            {
                return Results.Redirect(AuthSession.LinkFor(slug, role, $"login?err={ex.GateName}"));
            }
            return Results.Redirect(returnUrl);
        }).DisableAntiforgery().RequireAuth();

        // ─── Live unread counts — polled by JS in App.razor كُلّ ٢٠ ث ─────
        // يُحَدِّث الـ badges في الـ nav بِلا إعادَة تَحميل. مَنطِق العَدّ:
        //   - الرَسائِل: عَدَد المُحادَثات الَّتي فيها OwnerUnread/PartnerUnread
        //     لِلطَّرَف الَّذي = userId. الرَسائِل الَّتي أَرسَلَها المُستَخدِم
        //     لا تُحسَب لِأَنّ /send يَزيد عَدّاد الطَّرَف الآخَر فَقَط.
        //   - الإشعارات: عَدَد Notification بِـ IsRead=false.
        app.MapGet("/api/{slug}/unread-counts",
            async (string slug, HttpRequest req, IDocumentStore store) =>
        {
            var token = req.Cookies[AuthSession.CookieName(slug)];
            var parsed = AuthHandlers.ParseToken(token);
            if (parsed is null) return Results.Json(new { messages = 0, notifications = 0 });
            var (userId, tenantSlug, _) = parsed.Value;
            if (tenantSlug != slug) return Results.Json(new { messages = 0, notifications = 0 });

            await using var s = store.QuerySession(slug);
            var convs = await s.Query<Conversation>()
                .Where(c => c.OwnerId == userId || c.PartnerId == userId).ToListAsync();
            var messages = convs.Count(c =>
                (c.OwnerId == userId && c.OwnerUnread > 0) ||
                (c.PartnerId == userId && c.PartnerUnread > 0));
            var notifications = await s.Query<ACommerce.Kit.Notifications.Notification>()
                .CountAsync(n => n.UserId == userId && !n.IsRead);
            return Results.Json(new { messages, notifications });
        });

        // ─── Save driver area (anchor + radius) ─────────────────────────
        app.MapPost("/{slug}/me/area/save",
            async (string slug, HttpRequest req, IDocumentStore store) =>
        {
            var token = req.Cookies[AuthSession.CookieName(slug)];
            var parsed = AuthHandlers.ParseToken(token);
            if (parsed is null) return Results.Redirect(Link(req, slug, $"login"));
            var (userId, _, _) = parsed.Value;

            _ = double.TryParse(req.Form["anchor_lat"].ToString(),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var lat);
            _ = double.TryParse(req.Form["anchor_lng"].ToString(),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var lng);
            _ = int.TryParse(req.Form["radius"].ToString(), out var radius);
            if (radius < 0) radius = 0;
            if (radius > 500) radius = 500;

            await using var s = store.LightweightSession(slug);
            var user = await s.LoadAsync<User>(userId);
            if (user is null) return Results.Redirect(Link(req, slug, $"me"));
            user.AnchorLat = lat;
            user.AnchorLng = lng;
            user.RadiusKm  = radius;
            user.UpdatedAt = DateTime.UtcNow;
            s.Store(user);
            await s.SaveChangesAsync();
            return Results.Redirect(Link(req, slug, $"me/area?saved=1"));
        }).DisableAntiforgery();

        // ─── Start direct chat with another user ────────────────────────
        // مُستَخدَم في صَفحَة /{slug}/drivers — العَميل يَفتَح مُحادَثَة
        // مُباشَرَة مَع سائِق بِلا حاجَة لِنَشر طَلَب مِشوار.
        app.MapPost("/{slug}/users/{userId:guid}/chat",
            async (string slug, Guid userId, HttpRequest req, IDocumentStore store) =>
        {
            var token = req.Cookies[AuthSession.CookieName(slug)];
            var parsed = AuthHandlers.ParseToken(token);
            if (parsed is null) return Results.Redirect(Link(req, slug, $"login?returnUrl=/{slug}/drivers"));
            var (meId, tenantSlug, _) = parsed.Value;
            if (tenantSlug != slug) return Results.Redirect(Link(req, slug, $"login"));
            if (meId == userId) return Results.Redirect(Link(req, slug, $"drivers"));
            var meName = req.Cookies[AuthSession.CookieName(slug) + ".name"] ?? "أنا";

            await using var s = store.LightweightSession(slug);
            var partner = await s.LoadAsync<User>(userId);
            if (partner is null) return Results.Redirect(Link(req, slug, $"drivers"));

            // ابحَث عَن مُحادَثَة قائِمَة بَين الاثنَين (بِلا ListingId).
            var existing = (await s.Query<Conversation>()
                .Where(c => c.ListingId == null &&
                            ((c.OwnerId == meId && c.PartnerId == userId) ||
                             (c.OwnerId == userId && c.PartnerId == meId)))
                .ToListAsync()).FirstOrDefault();
            if (existing is not null)
                return Results.Redirect(Link(req, slug, $"chats/{existing.Id}"));

            var conv = new Conversation
            {
                Id = Guid.NewGuid(),
                OwnerId = meId, OwnerName = meName,
                PartnerId = partner.Id, PartnerName = partner.FullName,
                Subject = $"تَواصُل مَع {partner.FullName}",
                ListingId = null,
                LastAt = DateTime.UtcNow,
                // مُحادَثَة عامَّة بِلا TTL — لَيسَت مُؤَقَّتَة كَالمَشوار.
                ExpiresAt = null
            };
            s.Store(conv);
            await s.SaveChangesAsync();
            return Results.Redirect(Link(req, slug, $"chats/{conv.Id}"));
        }).DisableAntiforgery();

        // ─── Send chat message — gates: auth + terms ─────────────────────
        // boilerplate التَّوثيق المُتَكَرِّر استُبدِل بِـ .RequireAuth().RequireTerms()
        // — الـ filter يَكتُب userId إلى HttpContext.Items وَنَقرَأها هُنا.
        app.MapPost("/{slug}/chats/{conversationId:guid}/send",
            async (string slug, Guid conversationId, HttpContext http, HttpRequest req,
                   IDocumentStore store,
                   Microsoft.AspNetCore.SignalR.IHubContext<ACommerce.Kit.Realtime.Server.RealtimeHub> hub,
                   ACommerce.Templates.Customer.Marketplace.Services.WebPushService push) =>
        {
            var userId = http.UserId();

            var body = req.Form["body"].ToString().Trim();
            if (string.IsNullOrEmpty(body)) return Results.Redirect(Link(req, slug, $"chats/{conversationId}"));

            await using var s = store.LightweightSession(slug);
            var conv = await s.LoadAsync<Conversation>(conversationId);
            if (conv is null) return Results.Redirect(Link(req, slug, $"chats"));
            if (conv.OwnerId != userId && conv.PartnerId != userId) return Results.Forbid();
            if (conv.IsExpired) return Results.Redirect(Link(req, slug, $"chats/{conversationId}?err=expired"));

            var msg = new Message
            {
                Id = Guid.NewGuid(), ConversationId = conversationId,
                SenderId = userId, Body = body, SentAt = DateTime.UtcNow
            };
            s.Store(msg);
            conv.LastMessage = body.Length > 100 ? body[..100] : body;
            conv.LastAt = msg.SentAt;
            // أَنشِئ إشعاراً لِلطَّرَف الآخَر — يَظهَر في /notifications +
            // عَلى جَرَس الـ topnav. تَحَقُّق سَريع: لا تُكَرِّر إشعاراً عَلى
            // نَفس المُحادَثَة في آخِر ٣٠ ثانِيَة لِتَفادي السپام لَو أَرسَل
            // المُستَخدِم رَسائِل مُتَتالِيَة.
            var recipientId = userId == conv.OwnerId ? conv.PartnerId : conv.OwnerId;
            var senderName  = userId == conv.OwnerId ? conv.OwnerName  : conv.PartnerName;
            var since = DateTime.UtcNow.AddSeconds(-30);
            var hasRecent = await s.Query<ACommerce.Kit.Notifications.Notification>()
                .AnyAsync(n => n.UserId == recipientId &&
                               n.Type == "chat_message" &&
                               n.RelatedUrl == $"/{slug}/chats/{conversationId}" &&
                               n.At > since);
            if (!hasRecent)
            {
                s.Store(new ACommerce.Kit.Notifications.Notification
                {
                    Id = Guid.NewGuid(),
                    UserId = recipientId,
                    Type = "chat_message",
                    Title = $"رِسالَة مِن {senderName}",
                    Body = conv.LastMessage ?? "—",
                    RelatedUrl = $"/{slug}/chats/{conversationId}",
                    At = msg.SentAt
                });
            }
            if (userId == conv.OwnerId) conv.PartnerUnread++;
            else if (userId == conv.PartnerId) conv.OwnerUnread++;
            s.Store(conv);
            await s.SaveChangesAsync();
            await NudgeAsync(hub, slug, recipientId);
            if (!hasRecent)
                await push.SendAsync(store, slug, recipientId,
                    $"رِسالَة مِن {senderName}",
                    conv.LastMessage ?? "—",
                    url: $"/{slug}/chats/{conversationId}",
                    tag: $"chat-{conversationId}");
            return Results.Redirect(Link(req, slug, $"chats/{conversationId}"));
        }).DisableAntiforgery().RequireAuth().RequireTerms();

        // ─── Admin: create tenant ───────────────────────────────────────
        // نَموذَج SSR على /admin/tenants/new يُرسِل لِهُنا. عَلى الفَشَل نُعيد
        // إلى نَفس الصَفحَة مَع ?err=X و القِيَم المُدخَلَة لِيَحفَظها الـ form.
        app.MapPost("/admin/tenants/create",
            async (HttpRequest req, IDocumentStore store) =>
        {
            var f = req.Form;
            var slug    = f["slug"].ToString().Trim().ToLowerInvariant();
            var name    = f["name"].ToString().Trim();
            var tagline = f["tagline"].ToString().Trim();
            var color   = f["color"].ToString().Trim();
            var city    = f["city"].ToString().Trim();
            var channel = f["channel"].ToString().Trim();
            if (channel != "phone" && channel != "nafath") channel = "phone";
            var catsRaw = f["categories"].ToString();

            // ── سَلاسِل الإعادَة في حالَة الخَطَأ ──
            string Back(string err) => "/admin/tenants/new" + "?err=" + err
                + "&slug="     + Uri.EscapeDataString(slug)
                + "&name="     + Uri.EscapeDataString(name)
                + "&tagline="  + Uri.EscapeDataString(tagline)
                + "&color="    + Uri.EscapeDataString(color)
                + "&city="     + Uri.EscapeDataString(city)
                + "&channel="  + Uri.EscapeDataString(channel)
                + "&categories=" + Uri.EscapeDataString(catsRaw);

            // ── فَلتَرَة ──
            if (string.IsNullOrEmpty(slug) ||
                !System.Text.RegularExpressions.Regex.IsMatch(slug, "^[a-z0-9_-]+$"))
                return Results.Redirect(Back("slug_required"));
            if (string.IsNullOrEmpty(name))   return Results.Redirect(Back("name_required"));
            if (!System.Text.RegularExpressions.Regex.IsMatch(color, "^#[0-9A-Fa-f]{6}$"))
                return Results.Redirect(Back("color_invalid"));

            // ── الفِئات: كُلّ صَفّ "slug | label | icon | kind" ──
            var categories = new List<ACommerce.Kit.Tenants.Category>();
            var idx = 0;
            foreach (var line in catsRaw.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = line.Split('|', StringSplitOptions.TrimEntries);
                if (parts.Length < 2) return Results.Redirect(Back("bad_categories"));
                var cslug = parts[0].Trim().ToLowerInvariant();
                var clabel = parts[1].Trim();
                if (string.IsNullOrEmpty(cslug) || string.IsNullOrEmpty(clabel))
                    return Results.Redirect(Back("bad_categories"));
                categories.Add(new ACommerce.Kit.Tenants.Category
                {
                    Slug = cslug,
                    Label = clabel,
                    Icon  = parts.Length > 2 ? parts[2].Trim() : "🏠",
                    Kind  = parts.Length > 3 ? parts[3].Trim().ToLowerInvariant() : "",
                    SortOrder = idx++
                });
            }
            if (categories.Count == 0) return Results.Redirect(Back("no_categories"));

            // ── تَحَقُّق مِن عَدَم تَكرار الـ slug ──
            await using var s = store.LightweightSession();
            var existing = await s.LoadAsync<ACommerce.Kit.Tenants.Tenant>(slug);
            if (existing is not null) return Results.Redirect(Back("slug_taken"));

            // ── إنشاء ──
            s.Store(new ACommerce.Kit.Tenants.Tenant
            {
                Id          = slug,
                Name        = name,
                BrandColor  = color,
                TagLine     = tagline,
                City        = city,
                AuthChannel = channel,
                Categories  = categories,
                CreatedAt   = DateTime.UtcNow
            });
            await s.SaveChangesAsync();
            return Results.Redirect($"/admin");
        }).DisableAntiforgery();

        // ─── Admin: grant / revoke tenant_admin to a user ──────────────
        app.MapPost("/admin/tenants/{slug}/users/{userId:guid}/grant-admin",
            async (string slug, Guid userId, IDocumentStore store) =>
        {
            await using var g = store.QuerySession();
            var tenant = await g.LoadAsync<ACommerce.Kit.Tenants.Tenant>(slug);
            if (tenant is null ||
                !tenant.Roles.Any(r => r.CatalogSlug == "tenant_admin"))
                return Results.Redirect($"/admin/tenants/{slug}/users");
            await using var s = store.LightweightSession(slug);
            var user = await s.LoadAsync<User>(userId);
            if (user is null) return Results.Redirect($"/admin/tenants/{slug}/users");
            user.ActiveRole = "tenant_admin";
            user.UpdatedAt = DateTime.UtcNow;
            s.Store(user);
            await s.SaveChangesAsync();
            return Results.Redirect($"/admin/tenants/{slug}/users?saved=1");
        }).DisableAntiforgery();

        app.MapPost("/admin/tenants/{slug}/users/{userId:guid}/revoke-admin",
            async (string slug, Guid userId, IDocumentStore store) =>
        {
            await using var g = store.QuerySession();
            var tenant = await g.LoadAsync<ACommerce.Kit.Tenants.Tenant>(slug);
            if (tenant is null) return Results.Redirect($"/admin/tenants/{slug}/users");
            await using var s = store.LightweightSession(slug);
            var user = await s.LoadAsync<User>(userId);
            if (user is null) return Results.Redirect($"/admin/tenants/{slug}/users");
            // اِرجِع لِأَوَّل دَور غَير-إداريّ كَ افتراضي.
            var fallback = tenant.Roles.FirstOrDefault(r => r.CatalogSlug != "tenant_admin");
            user.ActiveRole = fallback?.Slug ?? "";
            user.UpdatedAt = DateTime.UtcNow;
            s.Store(user);
            await s.SaveChangesAsync();
            return Results.Redirect($"/admin/tenants/{slug}/users?saved=1");
        }).DisableAntiforgery();

        // ─── Admin: save roles ──────────────────────────────────────────
        // الـ form يُرسِل role_{catalogSlug}=1 لِكُلّ دَور مَختار + default_role
        // لِتَحديد الافتراضي. الـ Role يُنشَأ بِنَسخ القالِب مِن RoleCatalog
        // (Label/Icon/Permissions/Fields). إذا كانَ الدَور مَوجوداً مُسبَقاً
        // نَحتَفِظ بِالتَخصيصات (Label/Icon) لكِنّ نُحَدِّث Permissions/Fields
        // مِن الكاتالوج (لِيَستَفيد المَتجَر مِن تَحديثات الكاتالوج).
        app.MapPost("/admin/tenants/{slug}/roles/save",
            async (string slug, HttpRequest req, IDocumentStore store) =>
        {
            await using var s = store.LightweightSession();
            var t = await s.LoadAsync<ACommerce.Kit.Tenants.Tenant>(slug);
            if (t is null) return Results.Redirect("/admin");

            var defaultRole = req.Form["default_role"].ToString().Trim().ToLowerInvariant();
            var existingByCatalog = t.Roles
                .Where(r => !string.IsNullOrEmpty(r.CatalogSlug))
                .ToDictionary(r => r.CatalogSlug);

            var newRoles = new List<ACommerce.Kit.Roles.Role>();
            var idx = 0;
            foreach (var tmpl in ACommerce.Kit.Roles.RoleCatalog.All)
            {
                if (req.Form[$"role_{tmpl.Slug}"].ToString() != "1") continue;
                ACommerce.Kit.Roles.Role role;
                if (existingByCatalog.TryGetValue(tmpl.Slug, out var prev))
                {
                    // اِحفَظ تَخصيصات المُصَمِّم (Label/Icon لَو غُيِّرَت)
                    role = prev;
                    role.Permissions = tmpl.Permissions.ToList();
                    role.HomeRoute = tmpl.HomeRoute;
                    role.Fields = tmpl.Fields.Select(f => new ACommerce.Kit.Roles.RoleField
                    {
                        Code = f.Code, Label = f.Label, Type = f.Type,
                        IsRequired = f.IsRequired,
                        Options = f.Options.Select(o => new ACommerce.Kit.Roles.RoleFieldOption
                        {
                            Value = o.Value, Label = o.Label
                        }).ToList()
                    }).ToList();
                    role.SortOrder = idx++;
                }
                else
                {
                    role = ACommerce.Kit.Roles.RoleCatalog.InstantiateRole(tmpl, idx++);
                }
                role.IsDefault = defaultRole == tmpl.Slug;
                newRoles.Add(role);
            }

            t.Roles = newRoles;
            s.Store(t);
            await s.SaveChangesAsync();
            return Results.Redirect($"/admin/tenants/{slug}/roles?saved=1");
        }).DisableAntiforgery();

        // ─── Admin: save categories ─────────────────────────────────────
        // إعادَة كِتابَة قائِمَة الفِئات بِالكامِل (overwrite). الإعلانات
        // المَوجودَة بِفِئَة مَحذوفَة تَبقى في الـ events لكِن تَختَفي مِن
        // الواجِهَة — هذا قَرار صَريح في النَّص التَوضيحي.
        app.MapPost("/admin/tenants/{slug}/categories/save",
            async (string slug, HttpRequest req, IDocumentStore store) =>
        {
            var catsRaw = req.Form["categories"].ToString();
            string Back(string err) => $"/admin/tenants/{slug}/categories?err={err}";

            var categories = new List<ACommerce.Kit.Tenants.Category>();
            var idx = 0;
            foreach (var line in catsRaw.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var l = line.Trim();
                if (l.Length == 0) continue;
                var parts = l.Split('|', StringSplitOptions.TrimEntries);
                if (parts.Length < 2) return Results.Redirect(Back("bad_categories"));
                var cslug = parts[0].Trim().ToLowerInvariant();
                var clabel = parts[1].Trim();
                if (string.IsNullOrEmpty(cslug) || string.IsNullOrEmpty(clabel))
                    return Results.Redirect(Back("bad_categories"));
                categories.Add(new ACommerce.Kit.Tenants.Category
                {
                    Slug = cslug,
                    Label = clabel,
                    Icon  = parts.Length > 2 && !string.IsNullOrEmpty(parts[2]) ? parts[2].Trim() : "🏠",
                    Kind  = parts.Length > 3 ? parts[3].Trim().ToLowerInvariant() : "",
                    SortOrder = idx++
                });
            }
            if (categories.Count == 0) return Results.Redirect(Back("no_categories"));

            await using var s = store.LightweightSession();
            var t = await s.LoadAsync<ACommerce.Kit.Tenants.Tenant>(slug);
            if (t is null) return Results.Redirect("/admin");
            t.Categories = categories;
            s.Store(t);
            await s.SaveChangesAsync();
            return Results.Redirect($"/admin/tenants/{slug}?saved=1");
        }).DisableAntiforgery();

        // ─── Admin: save branding ───────────────────────────────────────
        app.MapPost("/admin/tenants/{slug}/branding/save",
            async (string slug, HttpRequest req, IDocumentStore store) =>
        {
            var name    = req.Form["name"].ToString().Trim();
            var tagline = req.Form["tagline"].ToString().Trim();
            var city    = req.Form["city"].ToString().Trim();
            var color   = req.Form["color"].ToString().Trim();
            var channel = req.Form["channel"].ToString().Trim();
            if (channel != "phone" && channel != "nafath") channel = "phone";

            if (string.IsNullOrEmpty(name))
                return Results.Redirect($"/admin/tenants/{slug}/branding?err=name_required");
            if (!System.Text.RegularExpressions.Regex.IsMatch(color, "^#[0-9A-Fa-f]{6}$"))
                return Results.Redirect($"/admin/tenants/{slug}/branding?err=color_invalid");

            await using var s = store.LightweightSession();
            var t = await s.LoadAsync<ACommerce.Kit.Tenants.Tenant>(slug);
            if (t is null) return Results.Redirect("/admin");
            t.Name = name;
            t.TagLine = tagline;
            t.City = city;
            t.BrandColor = color;
            t.AuthChannel = channel;
            s.Store(t);
            await s.SaveChangesAsync();
            return Results.Redirect($"/admin/tenants/{slug}?saved=1");
        }).DisableAntiforgery();

        // ─── Admin: save PWA per-role (name + custom icon) ──────────────
        // مُتَعَدِّد الـ parts (file upload). لِكُلّ دَور: name_{slug} +
        // icon_{slug} (مَلَفّ) + clear_{slug} (checkbox). الأَيقونَة تُحَوَّل
        // لِـ data: URL وَتُخزَّن مَعَ الدَور. سَقف ٢٥٦ كيلوبايت لِلحِفاظ
        // عَلى حَجم Tenant doc مَعقولاً.
        app.MapPost("/admin/tenants/{slug}/pwa/save",
            async (string slug, HttpRequest req, IDocumentStore store) =>
        {
            await using var s = store.LightweightSession();
            var t = await s.LoadAsync<ACommerce.Kit.Tenants.Tenant>(slug);
            if (t is null) return Results.Redirect("/admin");

            const long maxBytes = 256 * 1024;
            var allowed = new[] { "image/png", "image/svg+xml", "image/webp" };

            foreach (var r in t.Roles)
            {
                var nameInput = req.Form[$"name_{r.Slug}"].ToString().Trim();
                r.PwaName = string.IsNullOrEmpty(nameInput) ? null : nameInput;

                if (req.Form[$"clear_{r.Slug}"].ToString() == "1")
                    r.PwaIconDataUrl = null;

                var file = req.Form.Files[$"icon_{r.Slug}"];
                if (file is { Length: > 0 })
                {
                    if (file.Length > maxBytes)
                        return Results.Redirect($"/admin/tenants/{slug}/pwa?err=icon_too_large");
                    var ct = file.ContentType.ToLowerInvariant();
                    if (!allowed.Contains(ct))
                        return Results.Redirect($"/admin/tenants/{slug}/pwa?err=icon_bad_type");
                    using var ms = new MemoryStream();
                    await file.CopyToAsync(ms);
                    var b64 = Convert.ToBase64String(ms.ToArray());
                    r.PwaIconDataUrl = $"data:{ct};base64,{b64}";
                }
            }

            s.Store(t);
            await s.SaveChangesAsync();
            return Results.Redirect($"/admin/tenants/{slug}/pwa?saved=1");
        }).DisableAntiforgery();

        // ─── Admin: save regions ────────────────────────────────────────
        // اِحذِف كُلّ DiscoveryRegions الحالِيَّة لِلتَّينَنت ثُمّ أَعِد البِناء.
        // المَدينَة Level=1 (ParentId=null)، الحَيّ Level=2 (ParentId=cityId).
        app.MapPost("/admin/tenants/{slug}/regions/save",
            async (string slug, HttpRequest req, IDocumentStore store) =>
        {
            var raw = req.Form["regions"].ToString();
            if (string.IsNullOrWhiteSpace(raw))
                return Results.Redirect($"/admin/tenants/{slug}/regions?err=empty");

            var cities = new List<(string Name, List<string> Districts)>();
            foreach (var line in raw.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var l = line.Trim();
                if (l.Length == 0) continue;
                if (l.Contains('>'))
                {
                    var parts = l.Split('>', 2);
                    var cityName = parts[0].Trim();
                    if (string.IsNullOrEmpty(cityName))
                        return Results.Redirect($"/admin/tenants/{slug}/regions?err=bad_format");
                    var districts = parts[1]
                        .Split(new[] { '،', ',' },
                               StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Where(d => !string.IsNullOrEmpty(d))
                        .ToList();
                    cities.Add((cityName, districts));
                }
                else
                {
                    cities.Add((l, new List<string>()));
                }
            }
            if (cities.Count == 0)
                return Results.Redirect($"/admin/tenants/{slug}/regions?err=empty");

            await using var s = store.LightweightSession(slug);
            var existing = await s.Query<ImportedRecord>()
                .Where(r => r.Table == "DiscoveryRegions").ToListAsync();
            foreach (var r in existing) s.Delete(r);

            var now = DateTime.UtcNow;
            foreach (var (cityName, districts) in cities)
            {
                var cityId = Guid.NewGuid().ToString();
                s.Store(new ImportedRecord
                {
                    Id = $"DiscoveryRegions/{cityId}",
                    Table = "DiscoveryRegions",
                    SourceId = cityId,
                    ImportedAt = now,
                    Data = new Dictionary<string, object?>
                    {
                        ["Name"]     = cityName,
                        ["ParentId"] = null,
                        ["Level"]    = "1"
                    }
                });
                foreach (var d in districts)
                {
                    var distId = Guid.NewGuid().ToString();
                    s.Store(new ImportedRecord
                    {
                        Id = $"DiscoveryRegions/{distId}",
                        Table = "DiscoveryRegions",
                        SourceId = distId,
                        ImportedAt = now,
                        Data = new Dictionary<string, object?>
                        {
                            ["Name"]     = d,
                            ["ParentId"] = cityId,
                            ["Level"]    = "2"
                        }
                    });
                }
            }
            await s.SaveChangesAsync();
            return Results.Redirect($"/admin/tenants/{slug}/regions?saved=1");
        }).DisableAntiforgery();

        // ─── Admin: save attribute definitions for a scope ──────────────
        // الـ scope إمّا CategoryId (لِإعلانات تِلك الفِئَة) أَو
        // 00000000-0000-0000-0000-000000000F01 (sentinel البروفايل).
        // نُعيد كِتابَة CategoryAttributeMappings لِهذا الـ scope كامِلَة،
        // ونَنشُر AttributeDefinitions + AttributeValues جَديدَة. الـ defs
        // اليَتيمَة (لا scope آخَر يَستَخدِمها) تُحذَف لِتَنظيف الجَدول.
        app.MapPost("/admin/tenants/{slug}/attributes/save",
            async (string slug, HttpRequest req, IDocumentStore store) =>
        {
            var scopeStr = req.Form["scope"].ToString().Trim();
            var defsRaw  = req.Form["defs"].ToString();

            if (!Guid.TryParse(scopeStr, out var scopeId))
                return Results.Redirect($"/admin/tenants/{slug}/attributes?err=no_scope");

            string Back(string err) =>
                $"/admin/tenants/{slug}/attributes?scope={scopeId}&err={err}";

            var rows = new List<(string Code, string Name, string Type, bool Req,
                                 List<(string Val, string Label)> Opts)>();
            foreach (var line in defsRaw.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var l = line.Trim();
                if (l.Length == 0) continue;
                var parts = l.Split('|', StringSplitOptions.TrimEntries);
                if (parts.Length < 4) return Results.Redirect(Back("bad_format"));
                var code = parts[0];
                var name = parts[1];
                var type = parts[2];
                var req2 = parts[3].Equals("req", StringComparison.OrdinalIgnoreCase);
                if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(name) ||
                    string.IsNullOrEmpty(type))
                    return Results.Redirect(Back("bad_format"));
                var opts = new List<(string Val, string Label)>();
                if (parts.Length >= 5 && !string.IsNullOrEmpty(parts[4]))
                {
                    foreach (var pair in parts[4].Split(
                                 new[] { '،', ',' },
                                 StringSplitOptions.RemoveEmptyEntries |
                                 StringSplitOptions.TrimEntries))
                    {
                        var kv = pair.Split('=', 2);
                        if (kv.Length != 2) return Results.Redirect(Back("bad_format"));
                        opts.Add((kv[0].Trim(), kv[1].Trim()));
                    }
                }
                rows.Add((code, name, type, req2, opts));
            }

            await using var s = store.LightweightSession(slug);

            // اِجلِب كُلّ الـ Mappings والـ defs الحالِيَّة في الذاكِرَة
            // مَرَّة واحِدَة — أَسهَل لِفَلتَرَة الـ JsonElement يَدَويّاً.
            var allMappings = await s.Query<ImportedRecord>()
                .Where(r => r.Table == "CategoryAttributeMappings").ToListAsync();
            var allDefs = await s.Query<ImportedRecord>()
                .Where(r => r.Table == "AttributeDefinitions").ToListAsync();
            var allValues = await s.Query<ImportedRecord>()
                .Where(r => r.Table == "AttributeValues").ToListAsync();

            var scopeMappings = allMappings
                .Where(m => GuidFromData(m, "CategoryId") == scopeId).ToList();
            var defIdsInScope = scopeMappings
                .Select(m => GuidFromData(m, "AttributeDefinitionId"))
                .Where(g => g != Guid.Empty).Distinct().ToList();
            foreach (var m in scopeMappings) s.Delete(m);

            var stillUsedDefs = allMappings
                .Where(m => GuidFromData(m, "CategoryId") != scopeId)
                .Select(m => GuidFromData(m, "AttributeDefinitionId"))
                .ToHashSet();
            var orphans = defIdsInScope.Where(id => !stillUsedDefs.Contains(id)).ToHashSet();
            if (orphans.Count > 0)
            {
                foreach (var d in allDefs)
                    if (orphans.Contains(GuidFromData(d, "Id"))) s.Delete(d);
                foreach (var v in allValues)
                    if (orphans.Contains(GuidFromData(v, "AttributeDefinitionId"))) s.Delete(v);
            }

            var now = DateTime.UtcNow;
            var order = 0;
            foreach (var (code, name, type, req2, opts) in rows)
            {
                var defId = Guid.NewGuid();
                s.Store(new ImportedRecord
                {
                    Id = $"AttributeDefinitions/{defId}",
                    Table = "AttributeDefinitions",
                    SourceId = defId.ToString(),
                    ImportedAt = now,
                    Data = new Dictionary<string, object?>
                    {
                        ["Id"]         = defId.ToString(),
                        ["Code"]       = code,
                        ["Name"]       = name,
                        ["Type"]       = type,
                        ["IsRequired"] = req2 ? "true" : "false"
                    }
                });
                s.Store(new ImportedRecord
                {
                    Id = $"CategoryAttributeMappings/{defId}-{scopeId}",
                    Table = "CategoryAttributeMappings",
                    SourceId = $"{defId}-{scopeId}",
                    ImportedAt = now,
                    Data = new Dictionary<string, object?>
                    {
                        ["CategoryId"]            = scopeId.ToString(),
                        ["AttributeDefinitionId"] = defId.ToString(),
                        ["SortOrder"]             = order.ToString()
                    }
                });
                var voi = 0;
                foreach (var (val, label) in opts)
                {
                    var vid = Guid.NewGuid();
                    s.Store(new ImportedRecord
                    {
                        Id = $"AttributeValues/{vid}",
                        Table = "AttributeValues",
                        SourceId = vid.ToString(),
                        ImportedAt = now,
                        Data = new Dictionary<string, object?>
                        {
                            ["Id"]                    = vid.ToString(),
                            ["AttributeDefinitionId"] = defId.ToString(),
                            ["Value"]                 = val,
                            ["DisplayName"]           = label,
                            ["SortOrder"]             = voi.ToString()
                        }
                    });
                    voi++;
                }
                order++;
            }
            await s.SaveChangesAsync();
            return Results.Redirect($"/admin/tenants/{slug}/attributes?scope={scopeId}&saved=1");
        }).DisableAntiforgery();

        // ─── Admin: Agent — ask ─────────────────────────────────────────
        app.MapPost("/admin/agent/ask",
            async (HttpRequest req,
                   ACommerce.Templates.Customer.Marketplace.Services.AgentService agent) =>
        {
            var msg = req.Form["message"].ToString().Trim();
            if (string.IsNullOrEmpty(msg))
                return Results.Redirect("/admin/agent?err=empty#composer");
            await agent.AskAsync(msg);
            return Results.Redirect("/admin/agent#latest");
        }).DisableAntiforgery();

        // ─── Admin: Agent — apply a pending tool call ───────────────────
        app.MapPost("/admin/agent/tool/{toolId}/apply",
            async (string toolId,
                   ACommerce.Templates.Customer.Marketplace.Services.AgentService agent,
                   ACommerce.Templates.Customer.Marketplace.Services.AgentToolExecutor exec) =>
        {
            var session = await agent.LoadSessionAsync();
            var turn = session.Turns.LastOrDefault(t => t.Tool?.Id == toolId);
            if (turn?.Tool is null)
                return Results.Redirect("/admin/agent?err=tool_missing#latest");

            var (ok, msg) = await exec.ExecuteAsync(turn.Tool.Name, turn.Tool.InputJson);
            await agent.UpdateToolStatusAsync(toolId, ok ? "applied" : "error", msg);
            await agent.ContinueAfterToolAsync();
            return Results.Redirect("/admin/agent#latest");
        }).DisableAntiforgery();

        // ─── Admin: Agent — reject a pending tool call ──────────────────
        app.MapPost("/admin/agent/tool/{toolId}/reject",
            async (string toolId,
                   ACommerce.Templates.Customer.Marketplace.Services.AgentService agent) =>
        {
            await agent.UpdateToolStatusAsync(toolId, "rejected", null);
            await agent.ContinueAfterToolAsync();
            return Results.Redirect("/admin/agent#latest");
        }).DisableAntiforgery();

        // ─── Admin: Agent — reset conversation ──────────────────────────
        app.MapPost("/admin/agent/reset",
            async (ACommerce.Templates.Customer.Marketplace.Services.AgentService agent) =>
        {
            await agent.ResetAsync();
            return Results.Redirect("/admin/agent");
        }).DisableAntiforgery();

        // ─── Studio — مُصادَقَة وهميَّة + بَدء مِن صَفحَة الهبوط ──────────
        // صَفحَة الهبوط تُرسِل المُطالَبَة هُنا؛ نَحفَظها في cookie مُؤَقَّت
        // ثُمَّ نُحَوِّل لِلدُخول. بَعد الدُخول الناجِح نُنشِئ جَلسَة تَحليل
        // بِالمُطالَبَة ونُشَغِّلها.
        app.MapPost("/studio/begin", (HttpRequest req, HttpResponse res) =>
        {
            var prompt = req.Form["prompt"].ToString().Trim();
            if (!string.IsNullOrEmpty(prompt))
                res.Cookies.Append("ac.studio.prompt", Uri.EscapeDataString(prompt),
                    new CookieOptions { IsEssential = true, Path = "/",
                        Expires = DateTimeOffset.UtcNow.AddHours(2) });
            return Results.Redirect("/studio/auth");
        }).DisableAntiforgery();

        app.MapPost("/studio/auth/login", (HttpRequest req) =>
        {
            // وهميّ: لا إرسال SMS — نَنتَقِل مُباشَرَةً لِمَرحَلَة الرَّمز.
            var phone = req.Form["phone"].ToString().Trim();
            if (string.IsNullOrEmpty(phone))
                return Results.Redirect("/studio/auth?err=phone");
            return Results.Redirect($"/studio/auth?stage=verify&phone={Uri.EscapeDataString(phone)}");
        }).DisableAntiforgery();

        app.MapPost("/studio/auth/verify", async (
            HttpRequest req, HttpResponse res, IDocumentStore store,
            IServiceScopeFactory scopeFactory,
            Services.Incubator.FeasibilityAnalysisService incubator) =>
        {
            var phone = req.Form["phone"].ToString().Trim();
            var code  = req.Form["code"].ToString().Trim();
            var user = await Services.Incubator.StudioAuth.VerifyAsync(store, res, phone, code);
            if (user is null)
                return Results.Redirect(
                    $"/studio/auth?stage=verify&phone={Uri.EscapeDataString(phone)}&err=code");

            // مُطالَبَة مُعَلَّقَة؟ أَنشِئ جَلسَة وشَغِّل التَّحليل في الخَلفِيَّة.
            var promptCookie = req.Cookies["ac.studio.prompt"];
            if (!string.IsNullOrEmpty(promptCookie))
            {
                res.Cookies.Delete("ac.studio.prompt");
                var prompt = Uri.UnescapeDataString(promptCookie);
                var s = await incubator.StartAsync(user.Id, user.FullName);
                await incubator.SaveAnswerAsync(s.Id, "description", prompt);
                await incubator.MarkAnalyzingAsync(s.Id);
                _ = Task.Run(async () =>
                {
                    using var scope = scopeFactory.CreateScope();
                    var bg = scope.ServiceProvider
                        .GetRequiredService<Services.Incubator.FeasibilityAnalysisService>();
                    try { await bg.RunAnalysisAsync(s.Id); } catch { }
                });
                return Results.Redirect($"/studio/s/{s.Id}");
            }
            return Results.Redirect("/studio");
        }).DisableAntiforgery();

        app.MapPost("/studio/logout", (HttpResponse res) =>
        {
            Services.Incubator.StudioAuth.DeleteCookie(res);
            return Results.Redirect("/");
        }).DisableAntiforgery();

        // إعادَة تَحليل مِن داخِل لوحَة العميل (تُبقيه في مَساحَة /studio).
        app.MapPost("/studio/s/{id:guid}/analyze", async (
            Guid id, IServiceScopeFactory scopeFactory,
            Services.Incubator.FeasibilityAnalysisService svc) =>
        {
            await svc.MarkAnalyzingAsync(id);
            _ = Task.Run(async () =>
            {
                using var scope = scopeFactory.CreateScope();
                var bg = scope.ServiceProvider
                    .GetRequiredService<Services.Incubator.FeasibilityAnalysisService>();
                try { await bg.RunAnalysisAsync(id); } catch { }
            });
            return Results.Redirect($"/studio/s/{id}");
        }).DisableAntiforgery();

        // ─── Incubator — طبقة التحليل الاستثماري ─────────────────────────
        // الـ admin مفتوح حاليّاً، فالمالك = Guid ثابت (مجهول). الاكتشاف
        // SSR (POST لكل إجابة)، التحليل يُطلَق في الخلفية وصفحة الدراسة
        // تَستطلِع حتى يكتمل.
        app.MapPost("/admin/incubator/start",
            async (Services.Incubator.FeasibilityAnalysisService svc) =>
        {
            var s = await svc.StartAsync(Guid.Empty, "صاحِب المَشروع");
            return Results.Redirect($"/admin/incubator/{s.Id}");
        }).DisableAntiforgery();

        app.MapPost("/admin/incubator/{id:guid}/answer",
            async (Guid id, HttpRequest req, Services.Incubator.FeasibilityAnalysisService svc) =>
        {
            var qid = req.Form["questionId"].ToString().Trim();
            var answer = req.Form["answer"].ToString().Trim();
            if (!string.IsNullOrEmpty(qid))
                await svc.SaveAnswerAsync(id, qid, answer);
            return Results.Redirect($"/admin/incubator/{id}");
        }).DisableAntiforgery();

        app.MapPost("/admin/incubator/{id:guid}/analyze",
            async (Guid id, IServiceScopeFactory scopeFactory,
                   Services.Incubator.FeasibilityAnalysisService svc) =>
        {
            // عيّن الحالة فوراً (متزامن) لتعرض صفحة الدراسة المؤشّر،
            // ثم شغّل التحليل الطويل في الخلفية بنطاق DI جديد.
            await svc.MarkAnalyzingAsync(id);
            _ = Task.Run(async () =>
            {
                using var scope = scopeFactory.CreateScope();
                var bg = scope.ServiceProvider
                    .GetRequiredService<Services.Incubator.FeasibilityAnalysisService>();
                try { await bg.RunAnalysisAsync(id); }
                catch { /* الحالة تبقى Analyzing؛ تظهر مهلة في الواجهة */ }
            });
            return Results.Redirect($"/admin/incubator/{id}/study");
        }).DisableAntiforgery();

        // إعادة البدء = جلسة جديدة فارغة (الجلسة القديمة تبقى محفوظة).
        app.MapPost("/admin/incubator/restart",
            async (Services.Incubator.FeasibilityAnalysisService svc) =>
        {
            var s = await svc.StartAsync(Guid.Empty, "صاحِب المَشروع");
            return Results.Redirect($"/admin/incubator/{s.Id}");
        }).DisableAntiforgery();

        return app;
    }

    // اِستِخراج الدَور مِن Referer لِلطَلَبات POST الَّتي تَأتي مِن صَفحَة
    // داخِل /{slug}/r/{role}/... — نَستَخدِمه لِبِناء redirect role-aware.
    private static string? RoleFromReferer(HttpRequest req)
    {
        var referer = req.Headers["Referer"].ToString();
        if (string.IsNullOrEmpty(referer)) return null;
        try
        {
            var uri = new Uri(referer);
            return AuthSession.ExtractRoleFromPath(new PathString(uri.AbsolutePath));
        }
        catch { return null; }
    }

    private static string Link(HttpRequest req, string slug, string path)
        => AuthSession.LinkFor(slug, RoleFromReferer(req), path);

    // ─── PWA — manifest builder ──────────────────────────────────────
    private static async Task<IResult> BuildManifestAsync(
        string slug, string? role, IDocumentStore store)
    {
        await using var s = store.QuerySession();
        var tenant = await s.LoadAsync<ACommerce.Kit.Tenants.Tenant>(slug);
        if (tenant is null) return Results.NotFound();

        ACommerce.Kit.Roles.Role? r = null;
        if (!string.IsNullOrEmpty(role))
            r = tenant.Roles.FirstOrDefault(x => x.Slug == role);

        var prefix    = string.IsNullOrEmpty(role) ? $"/{slug}" : $"/{slug}/r/{role}";
        // الـ icon endpoint مُسَجَّل تَحت /api/… لا تَحت scope الـ PWA،
        // فَنُشير إليه بِالمَسار المُطلَق الصَحيح. تَركه تَحت prefix يُسَبِّب
        // 404 وَيُفشِل installability check (لا أَيقونات صالِحَة).
        var iconUrl   = string.IsNullOrEmpty(role)
            ? $"/api/{slug}/icon.svg"
            : $"/api/{slug}/r/{role}/icon.svg";
        var appName   = !string.IsNullOrEmpty(r?.PwaName) ? r!.PwaName!
                      : r is not null            ? $"{tenant.Name} — {r.Label}"
                                                 : tenant.Name;
        var shortName = r?.Label ?? tenant.Name;
        var shortcuts = BuildShortcuts(slug, role, r, iconUrl);

        return Results.Json(new
        {
            name = appName,
            short_name = shortName,
            description = tenant.TagLine,
            lang = "ar",
            dir = "rtl",
            id = $"{prefix}/",
            start_url = $"{prefix}/",
            scope = $"{prefix}/",
            display = "standalone",
            display_override = new[] { "window-controls-overlay", "standalone", "minimal-ui" },
            orientation = "any",
            background_color = "#f4f4f5",
            theme_color = tenant.BrandColor,
            launch_handler = new { client_mode = "navigate-existing" },
            // handle_links: "preferred" يُخبِر النِظام أَنّ هذه الـ PWA هي
            // المُعالِج المُفَضَّل لِلـ URLs داخِل scope. Chrome/Edge يَعرِضان
            // أَيقونَة "اِفتَح في التَّطبيق" في شَريط العُنوان عِندَ تَصَفُّح
            // عاديّ + يَفتَحان رَوابِط هذه النِطاق في الـ PWA إذا أَمكَن.
            handle_links = "preferred",
            icons = new object[]
            {
                // Chrome's installability checklist يَتَطَلَّب maskable + at-least
                // واحِد ≥ 192x192. SVG واحِدَة تُغَطّي كُلّ الأَحجام لكِنّ
                // نَذكُرها بِأَحجام مُحَدَّدَة لِيَقتَنِع المُتَصَفِّح.
                new { src = iconUrl, sizes = "192x192", type = "image/svg+xml", purpose = "any" },
                new { src = iconUrl, sizes = "512x512", type = "image/svg+xml", purpose = "any" },
                new { src = iconUrl + "?mask=1", sizes = "192x192", type = "image/svg+xml", purpose = "maskable" },
                new { src = iconUrl + "?mask=1", sizes = "512x512", type = "image/svg+xml", purpose = "maskable" },
                new { src = iconUrl, sizes = "any", type = "image/svg+xml", purpose = "any" }
            },
            shortcuts,
            categories = new[] { "business", "lifestyle", "productivity" },
            prefer_related_applications = false
        }, contentType: "application/manifest+json");
    }

    private static object[] BuildShortcuts(string slug, string? role,
        ACommerce.Kit.Roles.Role? r, string iconUrl)
    {
        // shortcuts حَسَب الدَور — مُختَصَرَات تَظهَر في long-press عَلى
        // الأَيقونَة (Android + Edge).
        var prefix = string.IsNullOrEmpty(role) ? $"/{slug}" : $"/{slug}/r/{role}";
        var icons  = new[] { new { src = iconUrl, sizes = "any", type = "image/svg+xml" } };
        return r?.CatalogSlug switch
        {
            "rider" => new object[]
            {
                new { name = "اِنشُر مِشواراً", short_name = "مِشوار", url = $"{prefix}/create-listing", icons },
                new { name = "طَلَباتي",      short_name = "طَلَباتي", url = $"{prefix}/me/listings",   icons },
                new { name = "السائِقون",     short_name = "سائِقون",  url = $"{prefix}/drivers",       icons }
            },
            "driver" or "shipper" => new object[]
            {
                new { name = "مَشاوير مُتاحَة", short_name = "مَشاوير", url = $"{prefix}/explore",      icons },
                new { name = "عُروضي",          short_name = "عُروضي",  url = $"{prefix}/me/offers",   icons },
                new { name = "مَنطِقَتي",       short_name = "مَنطِقَتي",url = $"{prefix}/me/area",    icons }
            },
            "vendor" or "host" => new object[]
            {
                new { name = "إعلان جَديد",    short_name = "إعلان",   url = $"{prefix}/create-listing", icons },
                new { name = "إعلاناتي",       short_name = "إعلاناتي",url = $"{prefix}/me/listings",   icons },
                new { name = "المُحادَثات",     short_name = "رَسائِل", url = $"{prefix}/chats",          icons }
            },
            "tenant_admin" => new object[]
            {
                new { name = "لَوحَة الإدارَة", short_name = "إدارَة",  url = $"{prefix}/manage", icons }
            },
            _ => new object[]
            {
                new { name = "اِستِكشاف",      short_name = "تَصَفُّح",url = $"{prefix}/explore",      icons },
                new { name = "حِسابي",        short_name = "حِسابي",  url = $"{prefix}/me",           icons }
            }
        };
    }

    // ─── PWA — icon builder (SVG ديناميكيّ) ───────────────────────────
    private static async Task<IResult> BuildIconAsync(
        string slug, string? role, IDocumentStore store)
    {
        await using var s = store.QuerySession();
        var tenant = await s.LoadAsync<ACommerce.Kit.Tenants.Tenant>(slug);
        if (tenant is null) return Results.NotFound();

        ACommerce.Kit.Roles.Role? r = null;
        if (!string.IsNullOrEmpty(role))
            r = tenant.Roles.FirstOrDefault(x => x.Slug == role);

        // أَيقونَة مُخَصَّصَة (data URL) → نَفُكّ الـ base64 وَنُقَدِّمها كَ صورَة.
        var custom = r?.PwaIconDataUrl;
        if (!string.IsNullOrEmpty(custom) && custom.StartsWith("data:"))
        {
            var comma = custom.IndexOf(',');
            if (comma > 0)
            {
                var meta = custom.Substring(5, comma - 5);   // "image/png;base64"
                var b64  = custom[(comma + 1)..];
                var contentType = meta.Split(';')[0];
                try { return Results.File(Convert.FromBase64String(b64), contentType); }
                catch { /* fall through to generated */ }
            }
        }

        // أَيقونَة مَولَّدَة: مُرَبَّع 512x512 بِلَون المَتجَر + الإيموجي/الحَرف.
        var color    = tenant.BrandColor;
        var emoji    = r?.Icon ?? tenant.Categories.FirstOrDefault()?.Icon ?? "";
        var initial  = (r?.Label ?? tenant.Name).FirstOrDefault().ToString();
        var label    = !string.IsNullOrEmpty(emoji) ? emoji : initial;
        var svg = $@"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 512 512"">
  <rect width=""512"" height=""512"" rx=""96"" fill=""{color}""/>
  <text x=""256"" y=""335"" text-anchor=""middle""
        font-family=""Cairo, Segoe UI Emoji, system-ui, sans-serif""
        font-size=""280"" font-weight=""700"" fill=""#ffffff"">{System.Net.WebUtility.HtmlEncode(label)}</text>
</svg>";
        return Results.Content(svg, "image/svg+xml; charset=utf-8");
    }

    // إشعار live بِأَنّ عَدّاد الغَير-مَقروء تَغَيَّر لِمُستَخدِم مُعَيَّن.
    // الـ client (JS في App.razor) يَستَمِع لِـ "unread_changed" عَلى hub
    // /realtime ويُحَدِّث الـ badges. آمِنَة لِلاستِدعاء حَتَّى لَو الـ hub
    // غَير مُتاح — اِلتِقاط الاستِثناء بِصَمت.
    private static async Task NudgeAsync(
        Microsoft.AspNetCore.SignalR.IHubContext<ACommerce.Kit.Realtime.Server.RealtimeHub> hub,
        string slug, Guid userId)
    {
        try
        {
            await hub.Clients
                .Group(ACommerce.Kit.Realtime.Server.RealtimeHub.GroupName(slug, userId))
                .SendAsync("unread_changed");
        }
        catch { /* لا نَكسِر تَدَفُّق الـ POST لَو SignalR فَشِل */ }
    }

    /// <summary>إنشاء إشعار لِكُلّ مُستَخدِم لَه دَور tenant_admin في هذا
    /// المَتجَر. يُستَدعَى عَلى أَحداث رَئيسيَّة (تَسجيل مُستَخدِم جَديد،
    /// إعلان جَديد، بَلاغ، …). لَو لا يُوجَد admin، يُتَجاهَل بِصَمت.</summary>
    private static async Task NotifyAdminsAsync(
        IDocumentStore store, string slug, string type,
        string title, string body, string relatedUrl,
        Microsoft.AspNetCore.SignalR.IHubContext<ACommerce.Kit.Realtime.Server.RealtimeHub>? hub = null,
        ACommerce.Templates.Customer.Marketplace.Services.WebPushService? push = null)
    {
        await using var s = store.LightweightSession(slug);
        var admins = await s.Query<User>()
            .Where(u => u.ActiveRole == "tenant_admin").ToListAsync();
        if (admins.Count == 0) return;

        var now = DateTime.UtcNow;
        foreach (var admin in admins)
        {
            s.Store(new ACommerce.Kit.Notifications.Notification
            {
                Id = Guid.NewGuid(),
                UserId = admin.Id,
                Type = type,
                Title = title,
                Body = body,
                RelatedUrl = relatedUrl,
                At = now
            });
        }
        await s.SaveChangesAsync();

        if (hub is not null)
            foreach (var admin in admins) await NudgeAsync(hub, slug, admin.Id);
        if (push is not null)
            foreach (var admin in admins)
                await push.SendAsync(store, slug, admin.Id, title, body,
                    url: relatedUrl, tag: $"admin-{type}-{Guid.NewGuid():N}");
    }

    // اِستِخراج owner_id مِن listing.Attributes كَ Guid.
    private static Guid? ParseListingOwnerId(Listing listing)
    {
        if (!listing.Attributes.TryGetValue("owner_id", out var s)) return null;
        return Guid.TryParse(s, out var g) ? g : null;
    }

    // فَحص صَلاحِيَّة لِلمُستَخدِم الحاليّ — يَجلِب tenant + user وَيُفَوِّض
    // إلى <see cref="ACommerce.Kit.Roles.RolePermissions.Has"/>.
    private static async Task<bool> HasPermissionAsync(
        string slug, Guid userId, string permission, IDocumentStore store)
    {
        await using var g = store.QuerySession();
        var tenant = await g.LoadAsync<ACommerce.Kit.Tenants.Tenant>(slug);
        if (tenant is null) return false;
        if (tenant.Roles.Count == 0) return true;   // legacy mode

        await using var t = store.QuerySession(slug);
        var user = await t.LoadAsync<ACommerce.Kit.Auth.User>(userId);
        if (user is null) return false;
        return ACommerce.Kit.Roles.RolePermissions.Has(
            tenant.Roles, user.ActiveRole, permission);
    }

    // تَسكين دَور لِمُستَخدِم بَعد تَوثيقِه — يُستَدعَى مِن /verify عِندَ
    // وُجود ?as=role مِن صَفحَة الدُخول. tenant_admin مَمنوع: يَجِب أَن
    // يُمنَح يَدَويّاً مِن /admin/tenants/{slug}/users.
    private static async Task AssignRoleAsync(
        string slug, Guid userId, string roleSlug, IDocumentStore store)
    {
        if (roleSlug == "tenant_admin") return;
        await using var g = store.QuerySession();
        var tenant = await g.LoadAsync<ACommerce.Kit.Tenants.Tenant>(slug);
        if (tenant is null) return;
        var picked = tenant.Roles.FirstOrDefault(r => r.Slug == roleSlug);
        if (picked is null) return;

        await using var s = store.LightweightSession(slug);
        var user = await s.LoadAsync<ACommerce.Kit.Auth.User>(userId);
        if (user is null) return;
        user.ActiveRole = roleSlug;
        user.UpdatedAt = DateTime.UtcNow;
        s.Store(user);
        await s.SaveChangesAsync();
    }

    // قَرار التَّوجيه بَعد دُخول ناجِح:
    //  1) مَتجَر بِلا أَدوار → الصَفحَة الرَّئيسِيَّة (سُلوك قَديم لِـ ashare/ejar).
    //  2) إن وُجِدَ <paramref name="asRole"/> (مِن ?as= أَو /r/role/login)
    //     → URL مَفروع تَحت /r/{role}/ لِيَفصِل الـ session.
    //  3) خِلاف ذلك: نَتَّبِع ActiveRole مِن user doc (legacy/no-prefix).
    private static async Task<string> PostLoginRouteAsync(
        string slug, Guid userId, string? asRole, IDocumentStore store)
    {
        await using var g = store.QuerySession();
        var tenant = await g.LoadAsync<ACommerce.Kit.Tenants.Tenant>(slug);
        if (tenant is null || tenant.Roles.Count == 0)
            return $"/{slug}";

        await using var t = store.LightweightSession(slug);
        var user = await t.LoadAsync<ACommerce.Kit.Auth.User>(userId);
        if (user is null) return $"/{slug}";

        // إن لَم يُعطَ asRole + لا ActiveRole + دَور واحِد → اِضبِطه تِلقائيّاً.
        if (string.IsNullOrEmpty(asRole) &&
            tenant.Roles.Count == 1 && string.IsNullOrEmpty(user.ActiveRole))
        {
            user.ActiveRole = tenant.Roles[0].Slug;
            t.Store(user);
            await t.SaveChangesAsync();
        }

        // الدَور الفِعليّ الَّذي سَنَستَخدِمُه لِلـ URL: asRole إن وُجِدَ، أَو
        // ActiveRole كَ احتِياط.
        var effectiveRoleSlug = !string.IsNullOrEmpty(asRole) ? asRole : user.ActiveRole;
        if (string.IsNullOrEmpty(effectiveRoleSlug))
            return $"/{slug}/me/role";

        var role = tenant.Roles.FirstOrDefault(r => r.Slug == effectiveRoleSlug);
        if (role is null) return $"/{slug}/me/role";

        // الـ onboarding مَطلوب لَو دَور لَه حُقول مَطلوبَة لَم تُملَأ بَعد.
        var roleValues = user.RoleAttributesJson.TryGetValue(role.Slug, out var rv)
            ? rv : new Dictionary<string, string>();
        var needsOnboarding = role.Fields
            .Where(f => f.IsRequired)
            .Any(f => !roleValues.ContainsKey(f.Code) || string.IsNullOrEmpty(roleValues[f.Code]));

        // عِندَ asRole نَبني URL مَفروع تَحت /r/{role}/ — يَضمَن أَنَّ
        // المُتَصَفِّح في هذا التَّبويب يَستَخدِم الـ cookie role-scoped.
        if (!string.IsNullOrEmpty(asRole))
        {
            if (needsOnboarding) return $"/{slug}/r/{asRole}/me/role/onboarding";
            return string.IsNullOrEmpty(role.HomeRoute)
                ? $"/{slug}/r/{asRole}"
                : $"/{slug}/r/{asRole}{role.HomeRoute}";
        }

        if (needsOnboarding) return $"/{slug}/me/role/onboarding";
        return string.IsNullOrEmpty(role.HomeRoute)
            ? $"/{slug}" : $"/{slug}{role.HomeRoute}";
    }

    // قِراءَة قِيمَة Guid مِن Dictionary مَع التَّعامُل مَع JsonElement
    // (Marten يَفُكّ التَسلسُل إلى JsonElement لِلقِيَم العامَّة).
    private static Guid GuidFromData(ImportedRecord r, string key)
    {
        if (!r.Data.TryGetValue(key, out var v) || v is null) return Guid.Empty;
        string? str = v is System.Text.Json.JsonElement el
            ? (el.ValueKind == System.Text.Json.JsonValueKind.String ? el.GetString() : el.ToString())
            : v.ToString();
        return Guid.TryParse(str, out var g) ? g : Guid.Empty;
    }
}
