using ACommerce.Kits.Auth.Operations;
using ACommerce.ServiceHost;
using Ashare.V3.Api.Realtime;
using Ashare.V3.Bootstrap;
using Ashare.V3.Data;
using Ashare.V3.Data.Stores;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Serilog;

// ═══════════════════════════════════════════════════════════════════════
// Ashare V3 API — يَستَهلِك asharedb (بَيانات حَيَّة V2 production).
// نَفس نَمَط Ejar.Api: AddACommerceServiceHost.AddKits(...) يَكشِف كُلّ
// الـ endpoints القياسيَّة (/version/check، /cities، /listings، /chats،
// /favorites، /auth/*) تِلقائيّاً. الفَرق فَقَط Stores — تَقرَأ مِن
// جَداوِل Ashare بَدَل Ejar (Law 6: Store = bridge).
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
    .UseRealtime<AshareV3SignalRTransport, AshareV3RealtimeHub>()
    .UseControllers()
    .RegisterEntities(
        typeof(AshareV3DbContext).Assembly,
        typeof(ACommerce.Kits.Discovery.Domain.DiscoveryRegion).Assembly)

    // الكيتس: Store-ها فَوق جَداوِل asharedb (Law 6).
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

builder.Services.AddSingleton<IUserIdProvider, AshareV3UserIdProvider>();

var app = builder.Build();

// Schema check (additive — يُنشِئ الجَداوِل الجَديدَة لَو ناقِصَة).
using (var scope = app.Services.CreateScope())
{
    try { await AshareV3Bootstrap.EnsureSchemaAsync(scope.ServiceProvider, builder.Configuration); }
    catch (Exception ex) { Log.Error(ex, "Ashare V3 bootstrap failed"); }
}

app.UseCors(opt => opt
    .SetIsOriginAllowed(_ => true).AllowAnyHeader().AllowAnyMethod().AllowCredentials());

app.UseACommerceServiceHost();

app.MapHub<AshareV3RealtimeHub>("/realtime", options =>
    options.Transports = HttpTransportType.ServerSentEvents | HttpTransportType.LongPolling);

Log.Information("Ashare V3 API ready [{Env}]", app.Environment.EnvironmentName);
app.Run();
