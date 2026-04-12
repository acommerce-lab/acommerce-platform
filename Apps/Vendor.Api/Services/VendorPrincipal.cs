using ACommerce.Authentication.Operations.Abstractions;

namespace Vendor.Api.Services;

/// <summary>
/// تطبيق IPrincipal لـ Vendor.Api.
/// </summary>
public class VendorPrincipal : IPrincipal
{
    public string UserId { get; init; } = default!;
    public string? DisplayName { get; init; }
    public IReadOnlyDictionary<string, string> Claims { get; init; } =
        new Dictionary<string, string>();
}
