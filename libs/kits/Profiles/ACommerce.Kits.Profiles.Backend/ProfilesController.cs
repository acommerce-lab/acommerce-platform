using ACommerce.Kits.Profiles.Operations;
using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;
using ACommerce.OperationEngine.Wire;
using ACommerce.OperationEngine.Wire.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ACommerce.Kits.Profiles.Backend;

/// <summary>
/// نقاط نهاية الـ profile الذاتيّ:
///   <c>GET /me/profile</c> — قراءة (returns IUserProfile).
///   <c>PUT /me/profile</c> — تعديل (PATCH semantics: null = "أبقِ القديم"
///   رغم HTTP verb).
/// </summary>
[ApiController]
[Authorize]
public sealed class ProfilesController : ControllerBase
{
    private readonly IProfileStore _store;
    private readonly OpEngine _engine;

    public ProfilesController(IProfileStore store, OpEngine engine)
    {
        _store = store; _engine = engine;
    }

    private string? CallerId =>
        User.FindFirst("user_id")?.Value
        ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    [HttpGet("/me/profile")]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        if (CallerId is null) return this.UnauthorizedEnvelope();
        var p = await _store.GetAsync(CallerId, ct);
        if (p is null) return this.UnauthorizedEnvelope("user_not_found");
        return this.OkEnvelope("profile.get", p);
    }

    public sealed record UpdateBody(string? FullName, string? Phone, string? Email, string? City, string? AvatarUrl);

    [HttpPut("/me/profile")]
    public async Task<IActionResult> Update([FromBody] UpdateBody req, CancellationToken ct)
    {
        if (CallerId is null) return this.UnauthorizedEnvelope();

        var patch = new ProfileUpdate(req.FullName, req.Phone, req.Email, req.City, req.AvatarUrl);
        var ok = false;
        var op = Entry.Create(ProfileOps.Update)
            .Describe($"User {CallerId} updates profile")
            .From($"User:{CallerId}", 1, ("role", "self"))
            .To($"User:{CallerId}",   1, ("role", "updated"))
            .Tag(ProfileTagKeys.UserId, CallerId)
            .Execute(async ctx =>
            {
                ok = await _store.UpdateNoSaveAsync(CallerId!, patch, ctx.CancellationToken);
            })
            .SaveAtEnd()
            .Build();

        var env = await _engine.ExecuteEnvelopeAsync(op, new { CallerId }, ct);
        if (env.Operation.Status != "Success")
            return this.BadRequestEnvelope(env.Operation.FailedAnalyzer ?? "update_failed", env.Operation.ErrorMessage);
        if (!ok) return this.UnauthorizedEnvelope("user_not_found");

        var fresh = await _store.GetAsync(CallerId!, ct);
        return this.OkEnvelope(ProfileOps.Update, (object?)fresh ?? new { id = CallerId });
    }
}
