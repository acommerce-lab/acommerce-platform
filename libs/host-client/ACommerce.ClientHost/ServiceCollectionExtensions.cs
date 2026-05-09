using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.ClientHost;

public static class ClientHostServiceCollectionExtensions
{
    /// <summary>
    /// نقطة الدخول الوحيدة. التطبيقات تستدعيها مرّةً واحدة في
    /// <c>Program.cs</c>/<c>MauiProgram.cs</c>:
    /// <code>
    /// services.AddACommerceClientHost(client => client
    ///     .AddKitPages(p => ...)
    ///     .AddDomainBindings(b => ...));
    /// </code>
    /// </summary>
    public static IServiceCollection AddACommerceClientHost(
        this IServiceCollection services,
        Action<ClientHostBuilder> configure)
    {
        var builder = new ClientHostBuilder(services);
        configure(builder);
        return services;
    }
}
