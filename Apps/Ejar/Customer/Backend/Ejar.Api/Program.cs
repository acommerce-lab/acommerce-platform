using System.Reflection;
using ACommerce.Kits.Auth;
using ACommerce.Kits.Auth.TwoFactor.AsAuth;
using ACommerce.Authentication.TwoFactor.Providers.Sms.Mock.Extensions;
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

// 2. Database (SQLite for dev)
var conn = builder.Configuration.GetConnectionString("Default") 
           ?? "Data Source=../../../../../data/ejar-customer-dev.db";

// تسجيل EjarDbContext مباشرة
builder.Services.AddDbContext<EjarDbContext>(options => options.UseSqlite(conn));
// تسجيله كـ DbContext عام للمستودعات
builder.Services.AddScoped<DbContext>(sp => sp.GetRequiredService<EjarDbContext>());
builder.Services.AddScoped<IRepositoryFactory, RepositoryFactory>();

// 3. Operation Engine & Interceptors
builder.Services.AddOperationEngine();
builder.Services.AddOperationInterceptors(registry => {
    registry.Register(new CrudActionInterceptor());
});
builder.Services.AddSingleton<IOperationInterceptor, OperationLogInterceptor>();

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
    if (db.Database.EnsureCreated())
    {
        Log.Information("Ejar.Db: schema ensured ({Provider})", db.Database.ProviderName);
        DbInitializer.Seed(db);
        Log.Information("Ejar.Db: seeding complete");
    }
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

Log.Information("Ejar API ready [{Env}]", app.Environment.EnvironmentName);
app.Run();
