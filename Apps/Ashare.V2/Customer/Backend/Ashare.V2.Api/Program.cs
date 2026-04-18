using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Interceptors;
using ACommerce.OperationEngine.Interceptors.Extensions;
using Ashare.V2.Api.Interceptors;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// CORS — السماح للواجهة بالاتصال محلياً
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

// ── OAM engine: كل mutation يمرّ عبر OpEngine.ExecuteEnvelopeAsync ──
builder.Services.AddScoped<OpEngine>(sp =>
    new OpEngine(sp, sp.GetRequiredService<ILogger<OpEngine>>()));

// ── Interceptors cross-cutting:
//      - ownership policy (must_own / must_not_own)
//      - listing quota (against ActiveSubscription.ListingsLimit)
builder.Services.AddOperationInterceptors(r =>
{
    r.Register(new OwnershipInterceptor());
    r.Register(new ListingQuotaInterceptor());
});

var app = builder.Build();

app.UseCors();
app.MapControllers();

app.Run();
