using Ashare.V3.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Ashare.V3.Api.Diagnostics;

/// <summary>
/// نقطة تشخيصيّة خاصّة بإيجار — تُظهر حالة الـ schema الفعليّة على الإنتاج.
/// مفيدة عندما تعود مسارات [Authorize] بـ 500: تَفحص هل الجداول موجودة،
/// هل الأعمدة الجديدة مطبَّقة، وأيّ migrations ناقصة. لا تَكشف بيانات
/// حسّاسة — فقط أسماء جداول/أعمدة + counts + applied/pending migrations.
///
/// <para>تَبقى AshareV3-specific لأنّ الـ counts تذكر <c>db.Listings</c>،
/// <c>db.Subscriptions</c> إلخ. التعميم سيتطلّب reflection على DbSet
/// — مكلف ولا يستحقّ.</para>
/// </summary>
public static class AshareV3DiagnosticsEndpoints
{
    public static IEndpointRouteBuilder MapAshareV3Diagnostics(this IEndpointRouteBuilder app)
    {
        app.MapGet("/diag/schema", async (AshareV3DbContext db) =>
        {
            object Try(Func<object> f)
            {
                try { return f(); }
                catch (Exception ex) { return new { error = ex.GetType().Name, message = ex.Message }; }
            }

            var applied = new List<string>();
            var pending = new List<string>();
            try
            {
                applied.AddRange(await db.Database.GetAppliedMigrationsAsync());
                pending.AddRange(await db.Database.GetPendingMigrationsAsync());
            }
            catch (Exception ex)
            {
                return Results.Json(new
                {
                    ok      = false,
                    error   = "migrations_history_unreadable",
                    message = ex.Message,
                    hint    = "DB قد بُنيت بـ EnsureCreated() — اضبط EJAR_DB_RESET=true مرّة وأعد التشغيل."
                });
            }

            return Results.Json(new
            {
                ok        = true,
                provider  = db.Database.ProviderName,
                canConnect = Try(() => (object)db.Database.CanConnect()),
                applied, pending,
                counts = new
                {
                    users         = Try(() => (object)db.Users.Count()),
                    listings      = Try(() => (object)db.Listings.Count()),
                    conversations = Try(() => (object)db.Conversations.Count()),
                    favorites     = Try(() => (object)db.Favorites.Count()),
                    plans         = Try(() => (object)db.Plans.Count()),
                    subscriptions = Try(() => (object)db.Subscriptions.Count()),
                    appVersions   = Try(() => (object)db.AppVersions.Count())
                }
            });
        }).AllowAnonymous();

        return app;
    }
}
