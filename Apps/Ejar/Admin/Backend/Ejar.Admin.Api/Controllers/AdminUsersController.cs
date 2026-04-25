using ACommerce.OperationEngine.Wire.Http;
using Ejar.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ejar.Admin.Api.Controllers;

/// <summary>
/// Admin view of users — list + activity counters. Mutation endpoints
/// (suspend/role) intentionally omitted in this MVP; add when policy is set.
/// </summary>
[ApiController]
[Authorize]
[Route("admin/users")]
public class AdminUsersController : ControllerBase
{
    [HttpGet]
    public IActionResult List()
    {
        var listingsByOwner = EjarSeed.Listings
            .GroupBy(l => l.OwnerId)
            .ToDictionary(g => g.Key, g => g.Count());

        var conversationsByPartner = EjarSeed.Conversations
            .GroupBy(c => c.PartnerId)
            .ToDictionary(g => g.Key, g => g.Count());

        // Derive the user set from public seed data (owners + partners). The
        // private _users dictionary in EjarSeed is the source of truth for profile
        // fields; we hydrate per id via GetUser, falling back to a minimal record.
        var ids = listingsByOwner.Keys
            .Concat(conversationsByPartner.Keys)
            .Distinct()
            .OrderBy(x => x);

        var users = ids.Select(id =>
        {
            var u = EjarSeed.GetUser(id);
            return new {
                id,
                fullName = u?.FullName ?? "(غير معروف)",
                phone    = u?.Phone,
                email    = u?.Email,
                city     = u?.City,
                listingsCount      = listingsByOwner.GetValueOrDefault(id, 0),
                conversationsCount = conversationsByPartner.GetValueOrDefault(id, 0)
            };
        }).ToList();
        return this.OkEnvelope("admin.user.list", users);
    }
}
