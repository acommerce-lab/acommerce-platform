using ACommerce.Authentication.TwoFactor.Operations;
using ACommerce.Authentication.TwoFactor.Operations.Abstractions;
using ACommerce.Authentication.TwoFactor.Providers.Sms.Mock.Extensions;
using ACommerce.OperationEngine.Core;
using ACommerce.SharedKernel.Abstractions.Entities;
using ACommerce.SharedKernel.Infrastructure.EFCores;
using ACommerce.SharedKernel.Infrastructure.EFCores.Extensions;
using Order.V2.Admin.Api;
using Order.V2.Domain;
using Serilog;

// ── Entity registration ────────────────────────────────────────────────────
EntityDiscoveryRegistry.RegisterEntity(typeof(User));
EntityDiscoveryRegistry.RegisterEntity(typeof(Category));
EntityDiscoveryRegistry.RegisterEntity(typeof(Vendor));
EntityDiscoveryRegistry.RegisterEntity(typeof(Offer));
EntityDiscoveryRegistry.RegisterEntity(typeof(OrderRecord));
EntityDiscoveryRegistry.RegisterEntity(typeof(OrderItem));
EntityDiscoveryRegistry.RegisterEntity(typeof(Conversation));
EntityDiscoveryRegistry.RegisterEntity(typeof(Order.V2.Domain.Message));
EntityDiscoveryRegistry.RegisterEntity(typeof(Notification));
EntityDiscoveryRegistry.RegisterEntity(typeof(Favorite));
EntityDiscoveryRegistry.RegisterEntity(typeof(TwoFactorChallengeRecord));
EntityDiscoveryRegistry.RegisterEntity(typeof(ACommerce.OrderPlatform.Entities.VendorUser));
EntityDiscoveryRegistry.RegisterEntity(typeof(ACommerce.OrderPlatform.Entities.VendorSettings));
EntityDiscoveryRegistry.RegisterEntity(typeof(ACommerce.OrderPlatform.Entities.WorkSchedule));
EntityDiscoveryRegistry.RegisterEntity(typeof(ACommerce.OrderPlatform.Entities.IncomingOrder));

var builder = WebApplication.CreateBuilder(args);
var cfg = builder.Configuration;
var env = builder.Environment;

// ── Database ───────────────────────────────────────────────────────────────
var dbProvider = cfg["Database:Provider"] ?? "SQLite";
var dbConnection = cfg["Database:ConnectionString"];

switch (dbProvider.ToLowerInvariant())
{
    case "sqlserver":
    case "mssql":
        if (string.IsNullOrWhiteSpace(dbConnection))
            Log.Warning("Order.V2: SQL Server selected but Database:ConnectionString is empty. Check your env vars.");
        else
            builder.Services.AddACommerceSqlServer(dbConnection);
        break;

    default: // SQLite
        var sqliteConn = !string.IsNullOrWhiteSpace(dbConnection)
            ? dbConnection
            : ACommerce.SharedKernel.Infrastructure.EFCores.PlatformDataRoot
                .SqliteConnectionString(builder.Environment.ContentRootPath, "order-v2.db");
        builder.Services.AddACommerceSQLite(sqliteConn);
        break;
}

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
var jwtIssuer  = builder.Configuration["JWT:Issuer"]   ?? "http://localhost:5103";
var jwtAud     = builder.Configuration["JWT:Audience"] ?? "order-v2-admin-api";
var jwtSecret  = builder.Configuration["JWT:SecretKey"] ?? "OrderV2-admin-dev-secret-min-32chars-here!!";
var jwtConfig  = new OrderV2AdminJwtConfig(jwtIssuer, jwtAud, jwtSecret);
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
    opt.AddPolicy("AdminOnly", p =>
        p.RequireAuthenticatedUser().RequireClaim("role", "admin")));

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
app.MapGet("/", () => Results.Ok(new { service = "Order.V2.Admin.Api", version = "2.0" }));
app.MapGet("/health", () => Results.Ok(new { status = "healthy", time = DateTime.UtcNow }));

// ── DB init + seed ─────────────────────────────────────────────────────────
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider
        .GetRequiredService<ACommerce.SharedKernel.Infrastructure.EFCores.Context.ApplicationDbContext>();
    if (ACommerce.SharedKernel.Infrastructure.EFCores.SqliteSchemaGuard.ResetIfDrifted(db))
        app.Logger.LogWarning("Order.V2.Admin.Api: schema drift — DB rebuilt");
    try { await db.Database.EnsureCreatedAsync(); }
    catch { app.Logger.LogInformation("Schema already created"); }
    ACommerce.SharedKernel.Infrastructure.EFCores.SqliteSchemaGuard.StampFingerprint(db);

    var repoFactory = scope.ServiceProvider
        .GetRequiredService<ACommerce.SharedKernel.Abstractions.Repositories.IRepositoryFactory>();
    var userRepo = repoFactory.CreateRepository<User>();
    var admins = await userRepo.GetAllWithPredicateAsync(u => u.Role == "admin");
    if (!admins.Any())
    {
        await userRepo.AddAsync(new User
        {
            Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow,
            PhoneNumber = "+966599999901", FullName = "Order V2 Admin",
            Role = "admin", IsActive = true
        }, default);
        app.Logger.LogInformation("Seeded Order.V2 demo admin (+966599999901)");
    }
}
catch (Exception ex) { app.Logger.LogError(ex, "DB init failed"); }

app.Run();
