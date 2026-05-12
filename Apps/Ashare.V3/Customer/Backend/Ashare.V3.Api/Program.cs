using ACommerce.Kits.Auth.Operations;
using ACommerce.Payments.Providers.Mock.Extensions;
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
                                 AccessTokenLifetimeDays: 30),
            auth => auth.AddTwoFactor(tf => tf.UseMockNafathProvider(opts =>
            {
                // قابِلَة لِلتَكوين مِن appsettings:MockNafath لاحِقاً؛ هُنا
                // افتِراضِيّات تَطوير: "00" يُعرَض لِلمُستَخدِم، تَحَقُّق تِلقائي
                // بَعد 5 ثَوانٍ.
                opts.DisplayCode       = "00";
                opts.AutoVerifySeconds = 5;
            })))
        .AddChat<AshareV3ChatStore>()
        .AddChatPresenceProbe<AshareV3ChatPresenceProbe>()
        .AddDiscovery()
        .AddFavorites<AshareV3FavoritesStore>()
        .AddVersions<AshareV3VersionStore>()
        .AddListings<AshareV3ListingStore>()
        .AddProfiles<AshareV3ProfileStore>()
        .AddReports<AshareV3ReportStore>())
        // Subscriptions kit مُعَلَّق حاليّاً — V3 لا يَستَخدِم باقات، الدَفع
        // بِحَسَب الإعلان الواحِد عَبر Mock Payment provider. عِند عَودَة
        // الباقات لاحِقاً، أَضِف AddSubscriptions هُنا + interceptor عَلى
        // <c>listing.create</c> يَفحَص اشتِراك المُستَخدِم. لا تَغيير في
        // الواجِهَة مَطلوب — الـ interceptor يَحجِب الـ op عِند غيابه.

    // التَّراكيب: غِراء عامّ بَين الكيتس (لا app-specific شَيء).
    // Chat.WithNotifications يُؤَجَّل حَتّى يُسَجَّل Notifications kit.
    .AddCompositions(c => c
        .Add<ACommerce.Compositions.Marketplace.MarketplaceComposition>())
);

builder.Services.AddSingleton<IUserIdProvider, AshareV3UserIdProvider>();

// مَصدَر القَوالِب الكانوني عِندَ تَوَفُّر بَيانات إنتاج. الـ controllers
// تَستَعمِله أَوَّلاً ⇒ fallback لِـ CategoryAttributeTemplates ⇒ fallback
// لِكود V3CategoryTemplates.
builder.Services.AddScoped<Ashare.V3.Data.Templates.ProductionAttributeTemplateSource>();
// كيت DynamicAttributes: نُسَجِّل نَفس المَصدَر تَحت واجِهَة الكيت
// لِيَتَّخِذه DynamicAttributesController + أَيّ مُستَهلِك آخَر.
builder.Services.AddScoped<ACommerce.Kits.DynamicAttributes.Backend.IAttributeTemplateSource>(sp =>
    sp.GetRequiredService<Ashare.V3.Data.Templates.ProductionAttributeTemplateSource>());
// MVC scan الافتِراضي = entry assembly فَقَط ⇒ نُلحِق Application Part
// لِالتِقاط DynamicAttributesController مَن كيت الخَلفِيَّة.
builder.Services.AddControllers()
    .AddApplicationPart(typeof(ACommerce.Kits.DynamicAttributes.Backend.DynamicAttributesController).Assembly);

// Mock payment gateway (dev/test). الإنتاج يَستَبدِله بِـ Moyasar/Noon.
// AutoCaptureSeconds=3 لِتَجرِبَة أَسرَع.
builder.Services.AddMockPayment(opts =>
{
    opts.AutoCaptureSeconds = 3;
});

// بَوّابَة الدَفع عَلى listing.create — interceptorان:
//   Pre  → يَتَحَقَّق فَقَط (يُرفِض إذا الدَفع ناقِص)
//   Post → يَستَهلِك (يَضَع Consumed=true) إذا نَجَحَت العَمَلِيَّة
// فَصلهُما يَضمَن أَنّ دَفعاً لا يُستَهلَك إلّا عِند حِفظ إعلان فِعليّاً.
// عَودَة Subscriptions تُضيف interceptor مُوازي يَفحَص الاشتِراك.
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<ACommerce.OperationEngine.Interceptors.IOperationInterceptor,
                              Ashare.V3.Api.Interceptors.ListingPaymentGateInterceptor>();
builder.Services.AddSingleton<ACommerce.OperationEngine.Interceptors.IOperationInterceptor,
                              Ashare.V3.Api.Interceptors.ListingPaymentConsumeInterceptor>();

// Enricher لِـ /listings/{id} — يُمَرِّر Images + Attributes (مَفكوكَة مِن
// AttributesJson) لِواجِهَة التَفاصيل. الكيت يَكتَشِفه عَبر DI.
builder.Services.AddScoped<ACommerce.Kits.Listings.Backend.IListingDetailEnricher,
                           Ashare.V3.Api.Enrichers.AshareV3ListingDetailEnricher>();

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
