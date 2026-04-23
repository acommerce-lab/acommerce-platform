using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;
using ACommerce.SharedKernel.Abstractions.Repositories;
using Microsoft.AspNetCore.Mvc;
using Order.V2.Api.Entities;

namespace Order.V2.Api.Controllers;

[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly IBaseAsyncRepository<User> _users;
    private readonly OpEngine _engine;

    public UsersController(IRepositoryFactory factory, OpEngine engine)
    {
        _users = factory.CreateRepository<User>();
        _engine = engine;
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

        var op = Entry.Create("user.update_profile")
            .Describe($"User:{id} updates profile")
            .From($"User:{id}", 1, ("role", "owner"))
            .To($"User:{id}", 1, ("role", "profile"))
            .Tag("user_id", id.ToString())
            .Execute(async ctx =>
            {
                if (req.FullName != null) u.FullName = req.FullName;
                if (req.Email != null) u.Email = req.Email;
                if (req.Theme != null) u.Theme = req.Theme;
                if (req.Language != null) u.Language = req.Language;
                if (req.CarModel != null) u.CarModel = req.CarModel;
                if (req.CarColor != null) u.CarColor = req.CarColor;
                if (req.CarPlate != null) u.CarPlate = req.CarPlate;
                u.UpdatedAt = DateTime.UtcNow;
                await _users.UpdateAsync(u, ctx.CancellationToken);
            })
            .Build();

        var result = await _engine.ExecuteAsync(op, ct);
        if (!result.Success) return this.BadRequestEnvelope("profile_update_failed", result.ErrorMessage);
        return this.OkEnvelope("user.update", u);
    }
}
