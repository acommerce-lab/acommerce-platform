using Ashare.V3.Bootstrap;
using Ashare.V3.Data;
using Microsoft.EntityFrameworkCore;

// ═══════════════════════════════════════════════════════════════════════
// Ashare V3 API — minimal bootstrap.
//
// هذا الـ Program مُتَعَمَّد المِنيمالِزم: يَتَّصِل بِـ asharedb، يَضمَن
// وُجود جَداوِل V3 الجَديدَة (Favorites/Reports/Notifications)، يَكشِف
// نُقطَتَي smoke test لِقِراءَة بَيانات حَيَّة.
//
// الكيتس (Auth, Listings, Chat, Subscriptions, Support, Notifications…)
// تُسَجَّل لاحِقاً كيت بِكيت بَعد كِتابَة Stores تُنَفِّذ واجِهاتها فَوق
// كَيانات Ashare V3. هذا الـ iteration الأَوَّل يَخدُم هَدَفاً واحِداً:
// ضَمان أَنّ المِعماريَّة مُتَوافِقَة مَع asharedb و أَنّ لا شَيء يَكسِر
// البَيانات الحَيَّة.
// ═══════════════════════════════════════════════════════════════════════

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAshareV3Database(builder.Configuration, builder.Environment);
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.SetIsOriginAllowed(_ => true).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

// ─── Schema check (additive only — لا تَعديل عَلى جَداوِل قائِمَة) ──
using (var scope = app.Services.CreateScope())
{
    try
    {
        await AshareV3Bootstrap.EnsureSchemaAsync(scope.ServiceProvider, builder.Configuration);
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetService<ILoggerFactory>()
                          ?.CreateLogger("Ashare.V3.Api");
        logger?.LogError(ex, "Bootstrap failed — API may not function correctly. Continuing anyway.");
    }
}

app.UseCors();
app.MapControllers();

// ─── Diagnostic endpoint — أَوَّل smoke test ──────────────────────
app.MapGet("/health", async (AshareV3DbContext db) => new
{
    status     = "ok",
    canConnect = await db.Database.CanConnectAsync(),
    profiles   = await db.Profiles.CountAsync(),
    listings   = await db.ProductListings.CountAsync(),
    chats      = await db.Chats.CountAsync(),
    messages   = await db.Messages.CountAsync()
});

// ─── Sample read endpoints — يَقرَأ بَيانات حَيَّة مِن asharedb ─────
app.MapGet("/api/listings", async (AshareV3DbContext db) =>
    await db.ProductListings
        .Where(l => l.IsActive)
        .OrderByDescending(l => l.CreatedAt)
        .Take(50)
        .Select(l => new
        {
            l.Id, l.Title, l.Price, l.City, l.FeaturedImage,
            l.IsFeatured, l.ViewCount, l.CreatedAt
        })
        .ToListAsync());

app.MapGet("/api/profiles", async (AshareV3DbContext db) =>
    await db.Profiles
        .OrderByDescending(p => p.CreatedAt)
        .Take(50)
        .Select(p => new
        {
            p.Id, p.FullName, p.PhoneNumber, p.City, p.IsVerified, p.CreatedAt
        })
        .ToListAsync());

// ─── Fallback لِكُلّ endpoint غَير مُسَجَّل بَعد ─────────────────────
// V3.Web يَستَهلِك Ejar V1 template الذي يَستَدعي /version/check و /cities و
// /home/view وغَيرها. هذه الكيتس لَم تُوصَل بَعد في V3.Api (iteration 1).
// نُرجِع JSON 501 (لا 404 فارِغ) لِيَتَمَكَّن الـ HttpDispatcher مِن الـ
// frontend مِن قِراءَة الجَواب دون JsonException.
app.MapFallback(() => Results.Json(
    new { error = "endpoint_not_implemented",
          message = "Ashare V3 iteration 1: kit endpoints not wired yet. " +
                    "Implement Stores + register kits in Program.cs in iteration 2+." },
    statusCode: 501));

app.Run();
