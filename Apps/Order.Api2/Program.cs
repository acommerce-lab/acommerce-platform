using ACommerce.Authentication.Operations;
using ACommerce.Authentication.Operations.Abstractions;
using ACommerce.Authentication.Providers.Token;
using ACommerce.Authentication.Providers.Token.Extensions;
using ACommerce.Authentication.TwoFactor.Operations;
using ACommerce.Authentication.TwoFactor.Operations.Abstractions;
using ACommerce.Authentication.TwoFactor.Providers.Sms.Extensions;
using ACommerce.Notification.Operations.Abstractions;
using ACommerce.Notification.Providers.InApp.Extensions;
using ACommerce.OperationEngine.Core;
using ACommerce.Realtime.Operations.Abstractions;
using ACommerce.Realtime.Providers.InMemory.Extensions;
using ACommerce.SharedKernel.Abstractions.Entities;
using ACommerce.SharedKernel.Infrastructure.EFCores.Extensions;
using Order.Api2.Entities;
using Order.Api2.Services;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ─────────────────────────────────────────────────────────
// Serilog
// ─────────────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();
builder.Host.UseSerilog();

// ─────────────────────────────────────────────────────────
// Register Order entities for entity discovery
// ─────────────────────────────────────────────────────────
EntityDiscoveryRegistry.RegisterEntity(typeof(User));
EntityDiscoveryRegistry.RegisterEntity(typeof(Category));
EntityDiscoveryRegistry.RegisterEntity(typeof(Vendor));
EntityDiscoveryRegistry.RegisterEntity(typeof(Offer));
EntityDiscoveryRegistry.RegisterEntity(typeof(OrderRecord));
EntityDiscoveryRegistry.RegisterEntity(typeof(OrderItem));
EntityDiscoveryRegistry.RegisterEntity(typeof(Conversation));
EntityDiscoveryRegistry.RegisterEntity(typeof(Message));
EntityDiscoveryRegistry.RegisterEntity(typeof(Notification));
EntityDiscoveryRegistry.RegisterEntity(typeof(Favorite));
EntityDiscoveryRegistry.RegisterEntity(typeof(TwoFactorChallengeRecord));

// ─────────────────────────────────────────────────────────
// Database (SQLite by default — file lives at ./data/order.db)
// ─────────────────────────────────────────────────────────
var dbProvider = builder.Configuration["Database:Provider"] ?? "SQLite";
var dbConnection = builder.Configuration["Database:ConnectionString"];

switch (dbProvider.ToLowerInvariant())
{
    case "sqlite":
        Directory.CreateDirectory("data");
        builder.Services.AddACommerceSQLite(dbConnection ?? "Data Source=data/order.db");
        break;
    default:
        builder.Services.AddACommerceInMemoryDatabase("OrderApi2Db");
        break;
}

// ─────────────────────────────────────────────────────────
// MVC + Swagger + CORS
// ─────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                     ?? new[] { "http://localhost:5701" };
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(p => p
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
});

// ─────────────────────────────────────────────────────────
// OperationEngine (Scoped)
// ─────────────────────────────────────────────────────────
builder.Services.AddScoped<OpEngine>(sp =>
    new OpEngine(sp, sp.GetRequiredService<ILogger<OpEngine>>()));

// ─────────────────────────────────────────────────────────
// Realtime + Notifications (in-memory)
// ─────────────────────────────────────────────────────────
builder.Services.AddInMemoryRealtimeTransport();
builder.Services.AddInAppNotificationChannel(opt =>
{
    opt.MethodName = "ReceiveNotification";
    opt.AllowOffline = true;
});

// ─────────────────────────────────────────────────────────
// Authentication: JWT + SMS 2FA (mock)
// ─────────────────────────────────────────────────────────
var jwtOptions = new JwtOptions
{
    Issuer = builder.Configuration["JWT:Issuer"] ?? "https://order.app",
    Audience = builder.Configuration["JWT:Audience"] ?? "order-api",
    SecretKey = builder.Configuration["JWT:SecretKey"]
        ?? "Order-dev-secret-do-not-use-in-prod-32chars-min!!!",
    AccessTokenLifetime = TimeSpan.TryParse(
        builder.Configuration["JWT:AccessTokenLifetime"], out var lt) ? lt : TimeSpan.FromDays(30)
};
builder.Services.AddSingleton(jwtOptions);
builder.Services.AddSingleton<JwtTokenStore>();
builder.Services.AddSingleton<ITokenValidator>(sp => sp.GetRequiredService<JwtTokenStore>());
builder.Services.AddSingleton<ITokenIssuer>(sp => sp.GetRequiredService<JwtTokenStore>());
builder.Services.AddTokenAuthenticator();

builder.Services.AddAuthentication(Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
                System.Text.Encoding.UTF8.GetBytes(jwtOptions.SecretKey)),
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddSingleton<AuthConfig>(sp =>
{
    var c = new AuthConfig();
    c.AddAuthenticator(sp.GetRequiredService<TokenAuthenticator>());
    c.UseIssuer(sp.GetRequiredService<ITokenIssuer>());
    return c;
});
builder.Services.AddScoped<AuthService>();

// 2FA: SMS (sandbox — code printed to logs)
builder.Services.AddSmsTwoFactor();
builder.Services.AddSingleton<TwoFactorConfig>(sp =>
{
    var cfg = new TwoFactorConfig();
    foreach (var ch in sp.GetServices<ITwoFactorChannel>())
        cfg.AddChannel(ch);
    return cfg;
});
builder.Services.AddScoped<TwoFactorService>();

// Seeder
builder.Services.AddScoped<OrderSeeder>();

// ─────────────────────────────────────────────────────────
// Build
// ─────────────────────────────────────────────────────────
var app = builder.Build();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

app.MapGet("/", () => Results.Ok(new
{
    service = "Order.Api2",
    description = "خدمة اوردر الخلفية المبنية على OperationEngine المحاسبي + SQLite",
    version = "1.0.0",
    docs = "/swagger"
}));

app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    time = DateTime.UtcNow,
    db = builder.Configuration["Database:Provider"] ?? "SQLite"
}));

// Create the DB schema (SQLite — picks up every entity registered above)
// then seed demo data.
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider
        .GetRequiredService<ACommerce.SharedKernel.Infrastructure.EFCores.Context.ApplicationDbContext>();
    await db.Database.EnsureCreatedAsync();
    Log.Information("Database schema ready");

    var seeder = scope.ServiceProvider.GetRequiredService<OrderSeeder>();
    await seeder.SeedAsync();
    Log.Information("Seeding complete");
}
catch (Exception ex)
{
    Log.Error(ex, "Database initialisation failed");
}

app.Run();
