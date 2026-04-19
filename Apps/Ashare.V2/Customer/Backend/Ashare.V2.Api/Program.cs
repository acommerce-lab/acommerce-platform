using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Interceptors;
using ACommerce.OperationEngine.Interceptors.Extensions;
using ACommerce.SharedKernel.Abstractions.Entities;
using Ashare.V2.Api.Entities;
using Ashare.V2.Api.Interceptors;
using Ashare.V2.Api.Middleware;
using Serilog;

// ─── Serilog ────────────────────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();

// ─── EntityDiscoveryRegistry ─────────────────────────────────────────────────
EntityDiscoveryRegistry.RegisterEntity(typeof(Profile));
EntityDiscoveryRegistry.RegisterEntity(typeof(TwoFactorChallengeRecord));
EntityDiscoveryRegistry.RegisterEntity(typeof(DeviceTokenEntity));
EntityDiscoveryRegistry.RegisterEntity(typeof(ProductCategory));
EntityDiscoveryRegistry.RegisterEntity(typeof(AttributeDefinition));
EntityDiscoveryRegistry.RegisterEntity(typeof(CategoryAttributeMapping));
EntityDiscoveryRegistry.RegisterEntity(typeof(Product));
EntityDiscoveryRegistry.RegisterEntity(typeof(ProductListing));
EntityDiscoveryRegistry.RegisterEntity(typeof(Order));
EntityDiscoveryRegistry.RegisterEntity(typeof(Booking));
EntityDiscoveryRegistry.RegisterEntity(typeof(Payment));
EntityDiscoveryRegistry.RegisterEntity(typeof(Subscription));
EntityDiscoveryRegistry.RegisterEntity(typeof(Chat));
EntityDiscoveryRegistry.RegisterEntity(typeof(ChatMessage));
EntityDiscoveryRegistry.RegisterEntity(typeof(Notification));
EntityDiscoveryRegistry.RegisterEntity(typeof(Complaint));
EntityDiscoveryRegistry.RegisterEntity(typeof(ComplaintReply));
EntityDiscoveryRegistry.RegisterEntity(typeof(LegalPage));
EntityDiscoveryRegistry.RegisterEntity(typeof(AttributionSession));

// ─── MVC + Swagger ──────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v2", new() { Title = "Ashare V2 API", Version = "v2" });
});

// ─── Health checks ───────────────────────────────────────────────────────────
builder.Services.AddHealthChecks();

// ─── CORS ────────────────────────────────────────────────────────────────────
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

// ─── OAM engine ──────────────────────────────────────────────────────────────
builder.Services.AddScoped<OpEngine>(sp =>
    new OpEngine(sp, sp.GetRequiredService<ILogger<OpEngine>>()));

// ─── Interceptors ────────────────────────────────────────────────────────────
builder.Services.AddSingleton<OperationLogInterceptor>();
builder.Services.AddOperationInterceptors(r =>
{
    r.Register(new OwnershipInterceptor());
    r.Register(new ListingQuotaInterceptor());
});
builder.Services.AddSingleton<ACommerce.OperationEngine.Interceptors.IOperationInterceptor>(
    sp => sp.GetRequiredService<OperationLogInterceptor>());

var app = builder.Build();

// ─── Middleware pipeline ──────────────────────────────────────────────────────
app.UseGlobalExceptionHandler();
app.UseCors();
app.UseCurrentUser();
app.UseCurrentCulture();

app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v2/swagger.json", "Ashare V2 API"));

app.MapControllers();
app.MapHealthChecks("/healthz");

app.Run();

// Allow integration tests
public partial class Program { }
