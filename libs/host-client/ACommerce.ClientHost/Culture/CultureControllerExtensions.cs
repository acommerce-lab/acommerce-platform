using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.ClientHost.Culture;

public static class CultureControllerExtensions
{
    /// <summary>
    /// يُسَجِّل <see cref="IClientCultureController"/> + <see cref="DefaultClientCultureController"/>.
    /// المُتَطَلَّب المُسَبَّق: <c>AddUiPreferences&lt;...&gt;</c> + <c>AddClientOpEngine</c>
    /// (لِيَتَوَفَّر <c>OpEngine</c>).
    /// </summary>
    public static IServiceCollection AddClientCultureController(this IServiceCollection services)
    {
        services.AddScoped<IClientCultureController, DefaultClientCultureController>();
        return services;
    }
}
