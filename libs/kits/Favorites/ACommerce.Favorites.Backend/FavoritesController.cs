using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ACommerce.Favorites.Operations.Entities;
using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;
using ACommerce.OperationEngine.Wire;
using ACommerce.OperationEngine.Wire.Http;
using ACommerce.OperationEngine.DataInterceptors;
using System.Security.Claims;

namespace ACommerce.Favorites.Backend;

/// <summary>
/// متحكّم المفضّلات. مسارات:
///   <c>GET  /favorites</c>                         — قائمة مفضّلات المستخدم.
///   <c>POST /listings/{id}/favorite</c>            — تبديل favorite على إعلان.
///   <c>GET  /api/favorites</c> (legacy، read-all)  — يبقى للـ data-interceptor flow.
///
/// <para>الـ kit يَكشف <see cref="IFavoritesStore"/>: التطبيق يَنفّذه ضدّ
/// كيان <c>Favorite</c>. <c>ToggleNoSaveAsync</c> يَتبع نمط H3 — tracker
/// فقط، الـ controller يضع <c>.SaveAtEnd()</c> على القيد ذرّيّاً.</para>
/// </summary>
[ApiController]
public class FavoritesController : ControllerBase
{
    private readonly OpEngine _engine;
    private readonly IFavoritesStore? _store;

    public FavoritesController(OpEngine engine, IFavoritesStore? store = null)
    {
        _engine = engine; _store = store;
    }

    private string? CallerId =>
        User.FindFirst("user_id")?.Value
        ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    // ─── GET /favorites ──────────────────────────────────────────────────
    [HttpGet("/favorites")]
    [Authorize(Policy = FavoritesKitPolicies.Authenticated)]
    public async Task<IActionResult> Mine(CancellationToken ct)
    {
        if (CallerId is null) return this.UnauthorizedEnvelope();
        if (_store is null)   return this.OkEnvelope("favorite.list", Array.Empty<object>());
        var rows = await _store.ListMineAsync(CallerId, ct);
        return this.OkEnvelope("favorite.list", rows);
    }

    // ─── POST /listings/{id}/favorite ────────────────────────────────────
    [HttpPost("/listings/{id:guid}/favorite")]
    [Authorize(Policy = FavoritesKitPolicies.Authenticated)]
    public async Task<IActionResult> ToggleListing(Guid id, CancellationToken ct)
    {
        if (CallerId is null) return this.UnauthorizedEnvelope();
        if (_store is null)
            return this.BadRequestEnvelope("favorites_store_not_registered");

        FavoriteToggleResult? result = null;
        var op = Entry.Create("favorite.toggle")
            .Describe($"User {CallerId} toggles favorite on Listing:{id}")
            .From($"User:{CallerId}", 1, ("role", "user"))
            .To($"Listing:{id}",      1, ("role", "favorited"))
            .Tag("entity_type", "Listing")
            .Tag("entity_id",   id.ToString())
            .Execute(async ctx =>
            {
                result = await _store.ToggleNoSaveAsync(CallerId!, "Listing", id.ToString(), ctx.CancellationToken);
            })
            .SaveAtEnd()
            .Build();

        var env = await _engine.ExecuteEnvelopeAsync(op, (object?)result ?? new { id }, ct);
        if (env.Operation.Status != "Success" || result is null)
            return this.BadRequestEnvelope(env.Operation.FailedAnalyzer ?? "toggle_failed", env.Operation.ErrorMessage);
        return this.OkEnvelope("favorite.toggle", result);
    }

    // ─── GET /api/favorites (legacy DataInterceptor read-all) ────────────
    // يَبقى لـ tooling/admin يَستهلك الـ generic CRUD path.
    [HttpGet("/api/favorites")]
    public async Task<IActionResult> ListAll()
    {
        var op = Entry.Create("favorite.list_all")
            .Tag(OperationTags.DbAction, DataOperationTypes.ReadAll)
            .Tag(OperationTags.TargetEntity, nameof(Favorite))
            .Build();

        var env = await _engine.ExecuteEnvelopeAsync(op, ctx =>
            ctx.Get<IReadOnlyList<Favorite>>("db_result"));

        return Ok(env);
    }
}
