using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.ClientHost.Auth;

/// <summary>
/// تَسجيل DI لِكامل machinery الـ Auth الـ client-side في سَطر واحد.
/// التَطبيق يُحَدِّد فقط ما يَخصّه (اسم scheme، storage key، اسم HttpClient):
/// <code>
/// services.AddClientAuth(o =&gt;
/// {
///     o.HttpClientName = "ejar";
///     o.StorageKey     = "ejar.v2.auth";
///     o.Scheme         = "EjarV2Auth";
/// });
/// </code>
/// </summary>
public static class ClientAuthServiceCollectionExtensions
{
    public static IServiceCollection AddClientAuth(
        this IServiceCollection services,
        Action<ClientAuthOptions> configure)
    {
        var opts = new ClientAuthOptions();
        configure(opts);
        if (string.IsNullOrEmpty(opts.HttpClientName))
            throw new InvalidOperationException("ClientAuthOptions.HttpClientName is required.");
        if (string.IsNullOrEmpty(opts.StorageKey))
            throw new InvalidOperationException("ClientAuthOptions.StorageKey is required.");
        if (string.IsNullOrEmpty(opts.Scheme))
            throw new InvalidOperationException("ClientAuthOptions.Scheme is required.");

        services.AddSingleton(new ClientAuthSchemeOptions(opts.Scheme));
        services.AddSingleton(new ClientAuthPersistenceOptions(opts.StorageKey));
        services.AddSingleton(new AuthenticatedHttpClientOptions(opts.HttpClientName));

        services.AddScoped<IClientAuthState, ClientAuthState>();
        services.AddScoped<IClientAuthPersistence, LocalStorageClientAuthPersistence>();
        services.AddScoped<AuthenticatedHttpClient>();

        services.AddAuthorizationCore();
        services.AddScoped<AuthenticationStateProvider, ClientAuthStateProvider>();

        return services;
    }
}

/// <summary>إعدادات Auth client-side. كلّ ما يَحتاج التَطبيق تَخصيصه.</summary>
public sealed class ClientAuthOptions
{
    /// <summary>اسم <c>IHttpClientFactory</c> logical client (مَثلاً "ejar").</summary>
    public string HttpClientName { get; set; } = "";

    /// <summary>مَفتاح localStorage (مَثلاً "ejar.v2.auth").</summary>
    public string StorageKey { get; set; } = "";

    /// <summary>اسم authentication scheme في <c>ClaimsIdentity</c>.</summary>
    public string Scheme { get; set; } = "";
}
