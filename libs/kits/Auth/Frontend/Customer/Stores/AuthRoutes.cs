using ACommerce.Client.Http;
using ACommerce.ClientHost.Operations;
using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.Kits.Auth.Frontend.Customer.Stores;

public static class AuthRoutesExtensions
{
    public static IServiceCollection AddAuthRoutes(this IServiceCollection services)
    {
        services.AddSingleton<IRoutesRegistrar, AuthRoutesRegistrar>();
        return services;
    }
}

internal sealed class AuthRoutesRegistrar : IRoutesRegistrar
{
    public void Register(HttpRouteRegistry routes)
    {
        routes.Map("auth.otp.request", HttpMethod.Post, "/auth/otp/request");
        routes.Map("auth.otp.verify",  HttpMethod.Post, "/auth/otp/verify");
        routes.Map("auth.sign_out",    HttpMethod.Post, "/auth/logout");
    }
}
