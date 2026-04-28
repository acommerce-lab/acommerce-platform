using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Wire;
using ACommerce.OperationEngine.Wire.Http;
using ACommerce.OperationEngine.DataInterceptors;
using ACommerce.OperationEngine.Patterns;
using Ejar.Api.Data;

namespace Ejar.Api.Controllers;

/// <summary>
/// متحكم الكتالوج الخاص بالمستخدم — يعتمد بالكامل على المعترض العام والتاجات القياسية.
/// الإشعارات تُقدَّم من Notifications Kit على /notifications، وليست هنا.
/// </summary>
[ApiController, Authorize, Route("api/catalog")]
public class CatalogController : ControllerBase
{
    private readonly OpEngine _engine;

    public CatalogController(OpEngine engine)
    {
        _engine = engine;
    }

    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue("user_id")!);

    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var op = Entry.Create("user.profile.get")
            .Tag(OperationTags.DbAction, DataOperationTypes.ReadById)
            .Tag(OperationTags.TargetEntity, nameof(UserEntity))
            .Build();

        op.Metadata["id"] = CurrentUserId;

        var env = await _engine.ExecuteEnvelopeAsync(op, ctx => {
            var u = ctx.Get<UserEntity>("db_result");
            if (u == null) return null;
            return new {
                id = u.Id, fullName = u.FullName, phone = u.Phone, email = u.Email,
                city = u.City, memberSince = u.MemberSince, avatar = u.AvatarUrl,
                stats = new { listingsCount = 5, bookingsCount = 12 }
            };
        });

        if (env.Data == null) return this.UnauthorizedEnvelope("user_not_found");
        return Ok(env);
    }

    [HttpGet("listings")]
    public async Task<IActionResult> MyListings()
    {
        var op = Entry.Create("user.listings.list")
            .Tag(OperationTags.DbAction, DataOperationTypes.ReadAll)
            .Tag(OperationTags.TargetEntity, nameof(ListingEntity))
            .Build();

        var env = await _engine.ExecuteEnvelopeAsync(op, ctx => {
            var items = ctx.Get<IReadOnlyList<ListingEntity>>("db_result");
            return items?.Where(l => l.OwnerId == CurrentUserId)
                .OrderByDescending(l => l.CreatedAt)
                .Select(l => new {
                    id = l.Id, title = l.Title, price = l.Price, timeUnit = l.TimeUnit,
                    status = l.Status, viewsCount = l.ViewsCount,
                    firstImage = l.ImagesCsv?.Split(',').FirstOrDefault()
                });
        });

        return Ok(env);
    }
}
