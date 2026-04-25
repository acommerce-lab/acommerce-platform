using ACommerce.OperationEngine.Wire.Http;
using Ejar.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ejar.Admin.Api.Controllers;

/// <summary>Read-only platform metrics for admins.</summary>
[ApiController]
[Authorize]
[Route("admin/dashboard")]
public class AdminDashboardController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        var listings = EjarSeed.Listings;
        var convs    = EjarSeed.Conversations;
        var data = new
        {
            totalListings    = listings.Count,
            verifiedListings = listings.Count(l => l.IsVerified),
            activeListings   = listings.Count(l => l.Status == 1),
            totalConversations = convs.Count,
            recentMessages   = convs.SelectMany(c => c.Messages)
                                     .OrderByDescending(m => m.SentAt)
                                     .Take(10)
                                     .Select(m => new { m.Id, m.ConversationId, m.From, m.SentAt }),
        };
        return this.OkEnvelope("admin.dashboard", data);
    }
}
