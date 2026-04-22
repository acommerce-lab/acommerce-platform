using ACommerce.Authentication.TwoFactor.Operations;
using ACommerce.Authentication.TwoFactor.Operations.Abstractions;
using ACommerce.Authentication.TwoFactor.Providers.Sms.Mock.Extensions;
using ACommerce.OperationEngine.Core;
using ACommerce.SharedKernel.Abstractions.Entities;
using ACommerce.SharedKernel.Infrastructure.EFCores;
using ACommerce.SharedKernel.Infrastructure.EFCores.Extensions;
using Order.V2.Vendor.Api;
using Order.V2.Api.Entities;

// ── Entity registration ────────────────────────────────────────────────────
EntityDiscoveryRegistry.RegisterEntity(typeof(User));
EntityDiscoveryRegistry.RegisterEntity(typeof(Category));
EntityDiscoveryRegistry.RegisterEntity(typeof(Vendor));
EntityDiscoveryRegistry.RegisterEntity(typeof(Offer));
EntityDiscoveryRegistry.RegisterEntity(typeof(OrderRecord));
EntityDiscoveryRegistry.RegisterEntity(typeof(OrderItem));
EntityDiscoveryRegistry.RegisterEntity(typeof(Conversation));
EntityDiscoveryRegistry.RegisterEntity(typeof(Order.V2.Api.Entities.Message));
EntityDiscoveryRegistry.RegisterEntity(typeof(Notification));
EntityDiscoveryRegistry.RegisterEntity(typeof(Favorite));
EntityDiscoveryRegistry.RegisterEntity(typeof(TwoFactorChallengeRecord));
EntityDiscoveryRegistry.RegisterEntity(typeof(ACommerce.OrderPlatform.Entities.VendorUser));
EntityDiscoveryRegistry.RegisterEntity(typeof(ACommerce.OrderPlatform.Entities.VendorSettings));
EntityDiscoveryRegistry.RegisterEntity(typeof(ACommerce.OrderPlatform.Entities.WorkSchedule));
EntityDiscoveryRegistry.RegisterEntity(typeof(ACommerce.OrderPlatform.Entities.IncomingOrder));

var builder = WebApplication.CreateBuilder(args);

// ── Database (shares order-v2.db with Order.V2.Api) ───────────────────────
var connStr = builder.Configuration["Database:ConnectionString"];
var dbConn = !string.IsNullOrWhiteSpace(connStr)
    ? connStr
    : ACommerce.SharedKernel.Infrastructure.EFCores.PlatformDataRoot
        .SqliteConnectionString(builder.Environment.ContentRootPath, "order-v2.db");
builder.Services.AddACommerceSQLite(dbConn);

// ── MVC (only this assembly) ───────────────────────────────────────────────
builder.Services.AddControllers()
    .PartManager.ApplicationParts.Clear();
var mvc = builder.Services.AddControllers();
mvc.PartManager.ApplicationParts.Add(
    new Microsoft.AspNetCore.Mvc.ApplicationParts.AssemblyPart(typeof(Program).Assembly));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ── CORS ───────────────────────────────────────────────────────────────────
var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

// ── OperationEngine ────────────────────────────────────────────────────────
builder.Services.AddScoped<OpEngine>(sp =>
    new OpEngine(sp, sp.GetRequiredService<ILogger<OpEngine>>()));

// ── JWT ────────────────────────────────────────────────────────────────────
var jwtIssuer = builder.Configuration["JWT:Issuer"]    ?? "http://localhost:5202";
var jwtAud    = builder.Configuration["JWT:Audience"]  ?? "order-v2-vendor-api";
var jwtSecret = builder.Configuration["JWT:SecretKey"] ?? "OrderV2-vendor-dev-secret-min-32chars-here!!";
var jwtConfig = new OrderV2VendorJwtConfig(jwtIssuer, jwtAud, jwtSecret);
builder.Services.AddSingleton(jwtConfig);

builder.Services.AddAuthentication(
    Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.MapInboundClaims = false;
        opt.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
                System.Text.Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer   = true,  ValidIssuer   = jwtIssuer,
            ValidateAudience = true,  ValidAudience = jwtAud,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization(opt =>
    opt.AddPolicy("VendorOnly", p =>
        p.RequireAuthenticatedUser().RequireClaim("role", "vendor")));

// ── 2FA SMS Mock ───────────────────────────────────────────────────────────
builder.Services.AddMockSmsTwoFactor();
builder.Services.AddSingleton<TwoFactorConfig>(sp =>
{
    var cfg = new TwoFactorConfig();
    foreach (var ch in sp.GetServices<ITwoFactorChannel>()) cfg.AddChannel(ch);
    return cfg;
});
builder.Services.AddScoped<TwoFactorService>();

// ── Build ──────────────────────────────────────────────────────────────────
var app = builder.Build();
if (!app.Environment.IsDevelopment()) app.UseExceptionHandler("/error");
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.MapGet("/", () => Results.Ok(new { service = "Order.V2.Vendor.Api", version = "2.0" }));
app.MapGet("/health", () => Results.Ok(new { status = "healthy", time = DateTime.UtcNow }));

// ── DB init (schema must already exist from Order.V2.Api seed) ─────────────
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider
        .GetRequiredService<ACommerce.SharedKernel.Infrastructure.EFCores.Context.ApplicationDbContext>();
    try { await db.Database.EnsureCreatedAsync(); }
    catch { app.Logger.LogInformation("Schema already created by Order.V2.Api"); }
    ACommerce.SharedKernel.Infrastructure.EFCores.SqliteSchemaGuard.StampFingerprint(db);
}
catch (Exception ex) { app.Logger.LogError(ex, "DB init failed"); }

app.Run();
