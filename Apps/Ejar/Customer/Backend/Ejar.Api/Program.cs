using System.Reflection;
using ACommerce.Kits.Auth;
using ACommerce.Kits.Auth.Operations.Extensions;
using ACommerce.Kits.Auth.TwoFactor.AsAuth;
using ACommerce.Authentication.TwoFactor.Providers.Sms.Mock.Extensions;
using ACommerce.Subscriptions.Operations.Extensions;
using ACommerce.Kits.Chat;
using ACommerce.Kits.Discovery.Backend;
using ACommerce.Realtime.Providers.InMemory.Extensions;
using ACommerce.Realtime.Providers.SignalR.Extensions;
using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Extensions;
using ACommerce.OperationEngine.Interceptors.Extensions;
using ACommerce.OperationEngine.Wire.Http;
using ACommerce.SharedKernel.Infrastructure.EFCores.Extensions;
using ACommerce.SharedKernel.Repositories.Interfaces;
using ACommerce.SharedKernel.Domain.Entities;
using ACommerce.Kits.Support.Domain;
using ACommerce.Kits.Discovery.Domain;
using ACommerce.Favorites.Operations.Entities;
using Ejar.Api.Data;
using Ejar.Api.Interceptors;
using Ejar.Api.Middleware;
using Microsoft.EntityFrameworkCore;
using Serilog;
using ACommerce.Kits.Support.Backend;
using ACommerce.Favorites.Backend;
using ACommerce.OperationEngine.DataInterceptors;
using ACommerce.OperationEngine.Interceptors;
using ACommerce.Realtime.Providers.SignalR;
using Ejar.Domain;
using Ejar.Api.Stores;
using ACommerce.Kits.Auth.Backend;
using ACommerce.Kits.Chat.Backend;
using ACommerce.Kits.Notifications.Backend;
using ACommerce.Kits.Versions.Backend;
using ACommerce.Kits.Auth.Operations;
using ACommerce.SharedKernel.Infrastructure.EFCores.Context;
using ACommerce.SharedKernel.Infrastructure.EFCore.Factories;

var builder = WebApplication.CreateBuilder(args);

// 1. Logging (Serilog)
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/ejar-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// 2. Database — provider يُختار حسب Database:Provider (sqlite/mssql)
// والـ connection string من Database:ConnectionString. الـ extension يحلّ
// مسار SQLite الافتراضي إلى <repo>/data أو ContentRoot عبر PlatformDataRoot.
builder.Services.AddEjarDatabase(builder.Configuration, builder.Environment);
// تسجيله كـ DbContext عام للمستودعات
builder.Services.AddScoped<DbContext>(sp => sp.GetRequiredService<EjarDbContext>());
builder.Services.AddScoped<IRepositoryFactory, RepositoryFactory>();

// 3. Operation Engine & Interceptors
builder.Services.AddOperationEngine();
builder.Services.AddOperationInterceptors(registry => {
    registry.Register(new CrudActionInterceptor());
});
builder.Services.AddSingleton<IOperationInterceptor, OperationLogInterceptor>();

// Cross-cutting gates — كلّها معترضات Pre تتجاوز عمليّاتها الذاتيّة.
// Versions يُسجَّل من AddVersionsKit أدناه. Auth يُسجَّل هنا.
// Subscriptions interceptor (لـ requires_subscription) متاح ولكن لم يُفعَّل
// في Ejar حالياً لأنّه لم يُسجَّل ISubscriptionProvider — يعمل تلقائياً
// لو أُضيف لاحقاً (المعترض يمرّ بسلام لو لم يُحقن المزوّد).
builder.Services.AddAuthGateInterceptor();
builder.Services.AddSubscriptionGateInterceptor();

// 4. MediatR
builder.Services.AddMediatR(cfg => {
    cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
});

// 5. Kits Registration
builder.Services.AddAuthKit<EjarCustomerAuthUserStore>(new AuthKitJwtConfig(
    "ejar_secret_key_12345678901234567890",
    "ejar.api",
    "ejar.mobile",
    "user",
    "User",
    30
))
    .AddMockSmsTwoFactor()
    .AddTwoFactorAsAuth();

builder.Services.AddSignalRRealtimeTransport()
    .AddInMemoryRealtimeTransport();
builder.Services.AddChatKit<EjarCustomerChatStore>();

builder.Services.AddDiscoveryKit();
builder.Services.AddSupportKit();
builder.Services.AddFavoritesKit();
builder.Services.AddNotificationsKit<EjarCustomerNotificationStore>();
builder.Services.AddVersionsKit<EjarVersionStore>();

// 6. Controllers & Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 7. Entity Discovery Registration
EntityDiscoveryRegistry.RegisterEntity<UserEntity>();
EntityDiscoveryRegistry.RegisterEntity<ListingEntity>();
EntityDiscoveryRegistry.RegisterEntity<ConversationEntity>();
EntityDiscoveryRegistry.RegisterEntity<MessageEntity>();
EntityDiscoveryRegistry.RegisterEntity<NotificationEntity>();
EntityDiscoveryRegistry.RegisterEntity<PlanEntity>();
EntityDiscoveryRegistry.RegisterEntity<SubscriptionEntity>();
EntityDiscoveryRegistry.RegisterEntity<InvoiceEntity>();
EntityDiscoveryRegistry.RegisterEntity<DiscoveryCategory>();
EntityDiscoveryRegistry.RegisterEntity<DiscoveryRegion>();
EntityDiscoveryRegistry.RegisterEntity<DiscoveryAmenity>();
EntityDiscoveryRegistry.RegisterEntity<Favorite>();
EntityDiscoveryRegistry.RegisterEntity<SupportTicket>();
EntityDiscoveryRegistry.RegisterEntity<SupportReply>();

var app = builder.Build();

// 8. DB Schema Ensure & Seed
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<EjarDbContext>();

    // نستخدم Migrate() بدل EnsureCreated() ليصبح schema drift قابلاً للإدارة:
    // أيّ تعديل في الكيانات → dotnet ef migrations add <Name> → النشر يطبّقها.
    // لـ DB جديدة: ينشئها ويطبّق كلّ migrations الموجودة.
    // لـ DB قديمة فيها __EFMigrationsHistory: يطبّق المتبقّي فقط.
    // لـ DB قديمة أُنشئت بـ EnsureCreated (لا history): يفشل Migrate لأنّ الجداول
    // موجودة — في هذه الحالة فرّغها مرّة واحدة (drop + create) وأعد التشغيل.
    var pending = db.Database.GetPendingMigrations().ToList();
    if (pending.Count > 0)
    {
        Log.Information("Ejar.Db: applying {Count} migration(s): {Names}",
            pending.Count, string.Join(", ", pending));
        db.Database.Migrate();
    }
    else
    {
        // قاعدة بيانات تُحسَب up-to-date. لو أُنشئت يدوياً أو بـ EnsureCreated
        // قد ينقصها جدول AppVersions الذي لم يُسجَّل في __EFMigrationsHistory.
        // helper idempotent يضمن وجوده.
        DbInitializer.EnsureAppVersionsTable(db);
    }

    // البذور: تعمل عند كلّ بدء تشغيل، تتفقّد بنفسها قبل الإدراج.
    if (!db.Users.Any())
    {
        Log.Information("Ejar.Db: seeding initial data");
        DbInitializer.Seed(db);
        Log.Information("Ejar.Db: seeding complete");
    }
    DbInitializer.SeedAppVersionsIfMissing(db);
}

// 9. Middleware Pipeline
app.UseMiddleware<GlobalExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors(opt => opt.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
app.UseAuthentication();
app.UseMiddleware<CurrentUserMiddleware>();
app.UseMiddleware<CurrentCultureMiddleware>();
app.UseAuthorization();

app.MapControllers();
app.MapHub<AShareHub>("/realtime");

// نقطة الفحص الحيّ التي يستدعيها سكربت api-diagnostics.js في الواجهات
// (Web/WASM/MAUI) عند بدء كلّ جلسة لاختبار الوصول إلى الخدمة وقاعدة البيانات.
// نُعرّفها بمسارَين: /healthz هو القياسيّ في k8s/Azure، /health للتوافق
// الخلفيّ مع أيّ عميل قديم استخدم الاسم بلا الـ z.
var healthHandler = (EjarDbContext db) =>
{
    var dbOk = false;
    try { dbOk = db.Database.CanConnect(); }
    catch { /* dbOk = false */ }
    return Results.Ok(new {
        status   = dbOk ? "healthy" : "degraded",
        db       = dbOk ? "ok" : "unreachable",
        time     = DateTime.UtcNow,
        service  = "Ejar.Api",
        provider = db.Database.ProviderName
    });
};
app.MapGet("/healthz", healthHandler).AllowAnonymous();
app.MapGet("/health",  healthHandler).AllowAnonymous();

Log.Information("Ejar API ready [{Env}]", app.Environment.EnvironmentName);
app.Run();
