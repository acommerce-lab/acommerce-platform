using ACommerce.Authentication.Operations.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.Authentication.Providers.Token.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// يسجل TokenAuthenticator كـ IAuthenticator.
    /// يجب تسجيل ITokenValidator مسبقاً في DI.
    ///
    ///   services.AddSingleton&lt;ITokenValidator, MyJwtValidator&gt;();
    ///   services.AddTokenAuthenticator();
    /// </summary>
    public static IServiceCollection AddTokenAuthenticator(this IServiceCollection services)
    {
        services.AddSingleton<TokenAuthenticator>();
        services.AddSingleton<IAuthenticator>(sp => sp.GetRequiredService<TokenAuthenticator>());
        return services;
    }
}
