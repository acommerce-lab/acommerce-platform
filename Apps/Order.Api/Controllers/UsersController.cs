using ACommerce.SharedKernel.Abstractions.Repositories;
using Microsoft.AspNetCore.Mvc;
using Order.Api.Entities;

namespace Order.Api.Controllers;

[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly IBaseAsyncRepository<User> _users;

    public UsersController(IRepositoryFactory factory)
    {
        _users = factory.CreateRepository<User>();
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var u = await _users.GetByIdAsync(id, ct);
        return u == null ? this.NotFoundEnvelope("user_not_found") : this.OkEnvelope("user.get", u);
    }

    public record UpdateProfileRequest(
        string? FullName,
        string? Email,
        string? Theme,
        string? Language,
        string? CarModel,
        string? CarColor,
        string? CarPlate);

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProfileRequest req, CancellationToken ct)
    {
        var u = await _users.GetByIdAsync(id, ct);
        if (u == null) return this.NotFoundEnvelope("user_not_found");
        if (req.FullName != null) u.FullName = req.FullName;
        if (req.Email != null) u.Email = req.Email;
        if (req.Theme != null) u.Theme = req.Theme;
        if (req.Language != null) u.Language = req.Language;
        if (req.CarModel != null) u.CarModel = req.CarModel;
        if (req.CarColor != null) u.CarColor = req.CarColor;
        if (req.CarPlate != null) u.CarPlate = req.CarPlate;
        u.UpdatedAt = DateTime.UtcNow;
        await _users.UpdateAsync(u, ct);
        return this.OkEnvelope("user.update", u);
    }
}
