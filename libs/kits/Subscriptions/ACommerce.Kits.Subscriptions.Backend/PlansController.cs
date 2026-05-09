using ACommerce.OperationEngine.Wire.Http;
using Microsoft.AspNetCore.Mvc;

namespace ACommerce.Kits.Subscriptions.Backend;

/// <summary>كاتالوج الباقات — مفتوح بلا توثيق (يحتاجه شريط الترقية).</summary>
[ApiController]
public sealed class PlansController : ControllerBase
{
    private readonly IPlanStore _store;
    public PlansController(IPlanStore store) => _store = store;

    [HttpGet("/plans")]
    public async Task<IActionResult> List(CancellationToken ct) =>
        this.OkEnvelope("plan.list", await _store.ListAsync(ct));
}
