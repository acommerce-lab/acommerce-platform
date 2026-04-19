using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Interceptors;
using ACommerce.OperationEngine.Interceptors.Extensions;
using Ashare.V2.Api.Interceptors;
using Ashare.V2.Api.Middleware;

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
builder.Services.AddSingleton<OperationLogInterceptor>();
builder.Services.AddOperationInterceptors(r =>
{
    r.Register(new OwnershipInterceptor());
    r.Register(new ListingQuotaInterceptor());
});
// Post-phase audit interceptor (اختياريّ — يُسجَّل كـ IOperationInterceptor
// فيُلتقَط تلقائياً من AddOperationInterceptors)
builder.Services.AddSingleton<ACommerce.OperationEngine.Interceptors.IOperationInterceptor>(
    sp => sp.GetRequiredService<OperationLogInterceptor>());

var app = builder.Build();

app.UseCors();
app.UseCurrentUser();       // يحقن user_id في HttpContext.Items
app.UseCurrentCulture();    // يقرأ Accept-Language / X-User-Timezone / X-User-Currency
app.MapControllers();

app.Run();
