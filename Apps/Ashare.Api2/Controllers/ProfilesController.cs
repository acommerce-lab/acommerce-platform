using ACommerce.SharedKernel.Abstractions.Repositories;
using Ashare.Api2.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Ashare.Api2.Controllers;

[ApiController]
[Route("api/profiles")]
public class ProfilesController : ControllerBase
{
    private readonly IBaseAsyncRepository<Profile> _profiles;
    private readonly IBaseAsyncRepository<User> _users;

    public ProfilesController(IRepositoryFactory factory)
    {
        _profiles = factory.CreateRepository<Profile>();
        _users    = factory.CreateRepository<User>();
    }

    [HttpGet("user/{userId:guid}")]
    public async Task<IActionResult> ByUser(Guid userId, CancellationToken ct)
    {
        var matches = await _profiles.GetAllWithPredicateAsync(p => p.UserId == userId);
        return matches.Count > 0
            ? this.OkEnvelope("profile.get", matches[0])
            : this.NotFoundEnvelope("profile_not_found");
    }

    public record UpsertProfileRequest(
        Guid UserId,
        string? FirstName,
        string? LastName,
        string? Bio,
        string? Gender,
        string? City,
        string? Country,
        string? PreferredLanguage,
        string? AvatarUrl,
        bool? IsPhonePublic,
        bool? IsEmailPublic);

    [HttpPut]
    public async Task<IActionResult> Upsert([FromBody] UpsertProfileRequest req, CancellationToken ct)
    {
        var user = await _users.GetByIdAsync(req.UserId, ct);
        if (user == null) return this.NotFoundEnvelope("user_not_found");

        var existing = await _profiles.GetAllWithPredicateAsync(p => p.UserId == req.UserId);
        Profile profile;

        if (existing.Count > 0)
        {
            profile = existing[0];
            profile.FirstName = req.FirstName ?? profile.FirstName;
            profile.LastName = req.LastName ?? profile.LastName;
            profile.Bio = req.Bio ?? profile.Bio;
            profile.Gender = req.Gender ?? profile.Gender;
            profile.City = req.City ?? profile.City;
            profile.Country = req.Country ?? profile.Country;
            profile.PreferredLanguage = req.PreferredLanguage ?? profile.PreferredLanguage;
            profile.AvatarUrl = req.AvatarUrl ?? profile.AvatarUrl;
            profile.IsPhonePublic = req.IsPhonePublic ?? profile.IsPhonePublic;
            profile.IsEmailPublic = req.IsEmailPublic ?? profile.IsEmailPublic;
            await _profiles.UpdateAsync(profile, ct);
        }
        else
        {
            profile = new Profile
            {
                Id = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow,
                UserId = req.UserId,
                FirstName = req.FirstName,
                LastName = req.LastName,
                Bio = req.Bio,
                Gender = req.Gender,
                City = req.City,
                Country = req.Country,
                PreferredLanguage = req.PreferredLanguage ?? "ar",
                AvatarUrl = req.AvatarUrl,
                IsPhonePublic = req.IsPhonePublic ?? true,
                IsEmailPublic = req.IsEmailPublic ?? false
            };
            await _profiles.AddAsync(profile, ct);
        }

        return this.OkEnvelope("profile.upsert", profile);
    }
}
