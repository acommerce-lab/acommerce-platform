using ACommerce.Authentication.Operations.Abstractions;

namespace Order.Api.Services;

/// <summary>
/// تطبيق IPrincipal لعشير.
/// </summary>
public class OrderPrincipal : IPrincipal
{
    public string UserId { get; init; } = default!;
    public string? DisplayName { get; init; }
    public IReadOnlyDictionary<string, string> Claims { get; init; } =
        new Dictionary<string, string>();
}
