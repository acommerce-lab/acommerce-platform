using ACommerce.Kits.Auth.Operations;
using ACommerce.ServiceHost;
using Ashare.V3.Bootstrap;
using Ashare.V3.Data;
using Ashare.V3.Data.Stores;
using Microsoft.EntityFrameworkCore;
using Serilog;

// ═══════════════════════════════════════════════════════════════════════
// Ashare V3 API — يَستَهلِك asharedb (بَيانات حَيَّة V2 production).
// نَفس نَمَط Ejar.Api: AddACommerceServiceHost.AddKits(...) يَكشِف كُلّ
// الـ endpoints القياسيَّة (/version/check، /cities، /listings، /chats،
// /favorites، /notifications، /complaints، /me، /auth/*) تِلقائيّاً.
// الفَرق فَقَط Stores — تَقرَأ مِن جَداوِل Ashare بَدَل Ejar.
// ═══════════════════════════════════════════════════════════════════════

var builder = WebApplication.CreateBuilder(args);

const string JwtSecret = "ashare_v3_secret_key_12345678901234567890";

builder.Services.AddAshareV3Database(builder.Configuration, builder.Environment);

builder.AddACommerceServiceHost(host => host
    .UseSerilog("ashare-v3")
    .UseDatabase<AshareV3DbContext>()
    .UseOperationEngine(typeof(Program).Assembly)
    .UseJwtAuthentication(jwt =>
    {
        jwt.Secret = JwtSecret;
        jwt.Issuer = "ashare.v3.api";
        jwt.Audience = "ashare.v3.mobile";
    })
    .UseControllers()
    .RegisterEntities(
        typeof(AshareV3DbContext).Assembly,
        typeof(ACommerce.Kits.Discovery.Domain.DiscoveryRegion).Assembly)

    // ── الكيتس: كُلّ كيت + Store-ها فَوق جَداوِل asharedb ───────────
    // (Support/Reports/Notifications/Subscriptions يَدخُلون بَعد إِضافَة
    //  view-records صَحيحَة لِكُلّ مِنها — تَنفيذ تالٍ.)
    .AddKits(kits => kits
        .AddAuth<AshareV3AuthUserStore>(
            new AuthKitJwtConfig(JwtSecret, "ashare.v3.api", "ashare.v3.mobile",
                                 Role: "user", PartyKind: "User",
                                 AccessTokenLifetimeDays: 30))
        .AddChat<AshareV3ChatStore>()
        .AddChatPresenceProbe<AshareV3ChatPresenceProbe>()
        .AddDiscovery()
        .AddFavorites<AshareV3FavoritesStore>()
        .AddVersions<AshareV3VersionStore>()
        .AddListings<AshareV3ListingStore>()
        .AddProfiles<AshareV3ProfileStore>())
);

var app = builder.Build();

// ─── Schema check (additive — يُنشِئ الجَداوِل الجَديدَة لَو ناقِصَة) ──
using (var scope = app.Services.CreateScope())
{
    try
    {
        await AshareV3Bootstrap.EnsureSchemaAsync(scope.ServiceProvider, builder.Configuration);
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Ashare V3 bootstrap failed");
    }
}

app.UseCors(opt => opt
    .SetIsOriginAllowed(_ => true).AllowAnyHeader().AllowAnyMethod().AllowCredentials());

app.UseACommerceServiceHost();

// ─── Smoke endpoint ───────────────────────────────────────────────────
app.MapGet("/health", async (AshareV3DbContext db) => new
{
    status     = "ok",
    canConnect = await db.Database.CanConnectAsync(),
    profiles   = await db.Profiles.CountAsync(),
    listings   = await db.ProductListings.CountAsync(),
    chats      = await db.Chats.CountAsync(),
    messages   = await db.Messages.CountAsync()
});

Log.Information("Ashare V3 API ready [{Env}]", app.Environment.EnvironmentName);
app.Run();
