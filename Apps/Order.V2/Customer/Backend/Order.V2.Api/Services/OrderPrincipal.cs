using ACommerce.Authentication.Operations.Abstractions;

namespace Order.V2.Api.Services;

public class OrderPrincipal : IPrincipal
{
    public string UserId { get; init; } = default!;
    public string? DisplayName { get; init; }
    public IReadOnlyDictionary<string, string> Claims { get; init; } =
        new Dictionary<string, string>();
}
