using ACommerce.OperationEngine.Wire.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ACommerce.Kits.Subscriptions.Backend;

/// <summary>فواتير المستخدم (read-only).</summary>
[ApiController]
[Authorize(Policy = SubscriptionsKitPolicies.Authenticated)]
public sealed class InvoicesController : ControllerBase
{
    private readonly IInvoiceStore _store;
    public InvoicesController(IInvoiceStore store) => _store = store;

    private string? CallerId =>
        User.FindFirst("user_id")?.Value
        ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    [HttpGet("/me/invoices")]
    public async Task<IActionResult> Mine(CancellationToken ct)
    {
        if (CallerId is null) return this.UnauthorizedEnvelope();
        return this.OkEnvelope("me.invoices", await _store.ListForUserAsync(CallerId, ct));
    }
}
