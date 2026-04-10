using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Interceptors;
using ACommerce.OperationEngine.Interceptors.Extensions;
using ACommerce.SharedKernel.Abstractions.Entities;
using ACommerce.SharedKernel.Abstractions.Repositories;
using ACommerce.SharedKernel.Infrastructure.EFCores.Extensions;
using Serilog;
using Vendor.Api.Entities;
using Vendor.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// ─── Serilog ─────────────────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();
builder.Host.UseSerilog();

// ─── Entity Discovery ────────────────────────────────────────────────────
EntityDiscoveryRegistry.RegisterEntity(typeof(VendorSettings));
EntityDiscoveryRegistry.RegisterEntity(typeof(WorkSchedule));
EntityDiscoveryRegistry.RegisterEntity(typeof(IncomingOrder));

// ─── Database (own SQLite — separate from Order.Api) ─────────────────────
Directory.CreateDirectory("data");
builder.Services.AddACommerceSQLite(
    builder.Configuration["Database:ConnectionString"] ?? "Data Source=data/vendor.db");

// ─── MVC + Swagger + CORS ────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:5801" };
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins(allowedOrigins)
     .AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

// ─── OperationEngine (Scoped) ────────────────────────────────────────────
builder.Services.AddScoped<OpEngine>(sp =>
    new OpEngine(sp, sp.GetRequiredService<ILogger<OpEngine>>()));

// ─── Interceptors ────────────────────────────────────────────────────────
// These fire on any operation tagged "vendor_order" (the receive endpoint tags it).
builder.Services.AddOperationInterceptors(registry =>
{
    // 1. Work Schedule Gate (Pre) — rejects if the vendor is currently closed
    registry.Register(new TaggedInterceptor(
        name: "WorkScheduleGate",
        watchedTag: "vendor_order",
        phase: InterceptorPhase.Pre,
        intercept: async (ctx, _) =>
        {
            var vendorIdStr = ctx.Operation.GetTagValue("vendor_id");
            if (string.IsNullOrEmpty(vendorIdStr) || !Guid.TryParse(vendorIdStr, out var vendorId))
                return AnalyzerResult.Pass("No vendor_id tag — skipping schedule check");

            var schedules = ctx.GetService<IRepositoryFactory>()
                .CreateRepository<WorkSchedule>();
            var now = DateTime.UtcNow;
            var todaySchedule = (await schedules.GetAllWithPredicateAsync(
                s => s.VendorId == vendorId && s.DayOfWeek == now.DayOfWeek))
                .FirstOrDefault();

            if (todaySchedule == null)
                return AnalyzerResult.Pass("No schedule configured — default open");

            if (todaySchedule.IsOff)
                return AnalyzerResult.Fail("المتجر مغلق اليوم (يوم إجازة)");

            var currentTime = now.ToString("HH:mm");
            if (string.Compare(currentTime, todaySchedule.OpenTime) < 0 ||
                string.Compare(currentTime, todaySchedule.CloseTime) > 0)
                return AnalyzerResult.Fail(
                    $"المتجر مغلق حالياً. ساعات العمل: {todaySchedule.OpenTime} – {todaySchedule.CloseTime}");

            return AnalyzerResult.Pass("المتجر مفتوح");
        },
        watchedValue: "receive" // only fires on order.receive, not accept/ready/deliver
    ));

    // 2. Acceptance Gate (Pre) — rejects if the vendor turned off order acceptance
    registry.Register(new TaggedInterceptor(
        name: "AcceptanceGate",
        watchedTag: "vendor_order",
        phase: InterceptorPhase.Pre,
        intercept: async (ctx, _) =>
        {
            var vendorIdStr = ctx.Operation.GetTagValue("vendor_id");
            if (string.IsNullOrEmpty(vendorIdStr) || !Guid.TryParse(vendorIdStr, out var vendorId))
                return AnalyzerResult.Pass();

            var settings = ctx.GetService<IRepositoryFactory>()
                .CreateRepository<VendorSettings>();
            var vendorSettings = (await settings.GetAllWithPredicateAsync(
                s => s.VendorId == vendorId)).FirstOrDefault();

            if (vendorSettings == null)
                return AnalyzerResult.Pass("No settings — default accepting");

            if (!vendorSettings.AcceptingOrders)
                return AnalyzerResult.Fail("المتجر أوقف استقبال الطلبات مؤقتاً");

            // Check max concurrent pending orders
            if (vendorSettings.MaxConcurrentPending > 0)
            {
                var orders = ctx.GetService<IRepositoryFactory>()
                    .CreateRepository<IncomingOrder>();
                var pendingCount = (await orders.GetAllWithPredicateAsync(
                    o => o.VendorId == vendorId && o.Status == IncomingOrderStatus.Pending)).Count;

                if (pendingCount >= vendorSettings.MaxConcurrentPending)
                    return AnalyzerResult.Fail(
                        $"المتجر وصل للحد الأقصى من الطلبات المعلقة ({vendorSettings.MaxConcurrentPending})");
            }

            return AnalyzerResult.Pass("المتجر يقبل طلبات");
        },
        watchedValue: "receive"
    ));

    // 3. Audit logger (Post) — logs every vendor operation
    registry.Register(new PredicateInterceptor(
        name: "VendorAuditLogger",
        phase: InterceptorPhase.Post,
        appliesTo: op => op.HasTag("vendor_order"),
        intercept: (ctx, _) =>
        {
            var logger = ctx.GetService<ILogger<Program>>();
            var parties = ctx.Operation.Parties;
            var from = parties.FirstOrDefault()?.Identity ?? "?";
            var to = parties.Skip(1).FirstOrDefault()?.Identity ?? "?";
            logger.LogInformation(
                "[AUDIT] {Type} From={From} To={To} Status={Status}",
                ctx.Operation.Type, from, to,
                ctx.Operation.Status);
            return Task.FromResult(AnalyzerResult.Pass());
        }));
});

// ─── HTTP Client → Order.Api (for callbacks) ─────────────────────────────
var orderApiBase = builder.Configuration["OrderApi:BaseUrl"] ?? "http://localhost:5101";
builder.Services.AddHttpClient("order-api", c =>
{
    c.BaseAddress = new Uri(orderApiBase);
    c.Timeout = TimeSpan.FromSeconds(15);
});
builder.Services.AddScoped<OrderApiCallback>(sp =>
{
    var factory = sp.GetRequiredService<IHttpClientFactory>();
    var logger = sp.GetRequiredService<ILogger<OrderApiCallback>>();
    return new OrderApiCallback(factory.CreateClient("order-api"), logger);
});

// ─── Background: Order timeout auto-cancel ───────────────────────────────
builder.Services.AddHostedService<OrderTimeoutService>();

// ─── Seeder ──────────────────────────────────────────────────────────────
builder.Services.AddScoped<VendorSeeder>();

// ─── Build ───────────────────────────────────────────────────────────────
var app = builder.Build();

app.UseCors();
app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();

app.MapGet("/", () => Results.Ok(new
{
    service = "Vendor.Api",
    description = "خدمة التاجر — إدارة الطلبات والجداول والإعدادات عبر OpEngine",
    version = "1.0.0",
    docs = "/swagger"
}));

app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    time = DateTime.UtcNow,
    db = "SQLite (data/vendor.db)"
}));

// ─── DB init + seed ──────────────────────────────────────────────────────
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider
        .GetRequiredService<ACommerce.SharedKernel.Infrastructure.EFCores.Context.ApplicationDbContext>();
    await db.Database.EnsureCreatedAsync();
    Log.Information("Vendor.Api database ready");

    var seeder = scope.ServiceProvider.GetRequiredService<VendorSeeder>();
    await seeder.SeedAsync();
    Log.Information("Vendor.Api seeding complete");
}
catch (Exception ex)
{
    Log.Error(ex, "Vendor.Api database init failed");
}

app.Run();
