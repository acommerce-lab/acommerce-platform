namespace ACommerce.Client.Http;

/// <summary>
/// تعيين نوع العملية → URL/method.
/// كل مكتبة domain تسجّل routes الخاصة بها هنا.
///
/// مثال:
///   routes.Map("listing.create", HttpMethod.Post, "/api/listings");
///   routes.Map("auth.signin", HttpMethod.Post, "/api/auth/sms/verify");
/// </summary>
public class HttpRouteRegistry
{
    private readonly Dictionary<string, HttpRoute> _routes = new();

    public void Map(string operationType, HttpMethod method, string urlTemplate)
    {
        _routes[operationType] = new HttpRoute(method, urlTemplate);
    }

    public HttpRoute? Resolve(string operationType)
        => _routes.TryGetValue(operationType, out var r) ? r : null;

    public IReadOnlyDictionary<string, HttpRoute> All => _routes;
}

public record HttpRoute(HttpMethod Method, string UrlTemplate);
