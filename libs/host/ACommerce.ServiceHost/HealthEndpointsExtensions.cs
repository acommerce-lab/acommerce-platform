using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.ServiceHost;

/// <summary>
/// نقاط health عامّة قابلة للتعميم — تُستهلكها سكربتات api-diagnostics.js
/// في الواجهات لاختبار الوصول للخدمة الخلفيّة وقاعدة البيانات.
/// </summary>
public static class HealthEndpointsExtensions
{
    /// <summary>
    /// يكشف <c>GET /healthz</c> + <c>GET /health</c>:
    /// <code>
    /// { status, db, time, service, provider }
    /// </code>
    /// كلاهما <c>AllowAnonymous</c>. <c>/health</c> alias تاريخيّ
    /// (k8s/Azure يستعملان <c>/healthz</c>).
    /// </summary>
    public static IEndpointRouteBuilder MapHealthEndpoints<TDbContext>(
        this IEndpointRouteBuilder app, string serviceName)
        where TDbContext : DbContext
    {
        var handler = (TDbContext db) =>
        {
            var dbOk = false;
            try { dbOk = db.Database.CanConnect(); } catch { /* dbOk = false */ }
            return Results.Ok(new
            {
                status   = dbOk ? "healthy" : "degraded",
                db       = dbOk ? "ok"      : "unreachable",
                time     = DateTime.UtcNow,
                service  = serviceName,
                provider = db.Database.ProviderName
            });
        };
        app.MapGet("/healthz", handler).AllowAnonymous();
        app.MapGet("/health",  handler).AllowAnonymous();
        return app;
    }
}
