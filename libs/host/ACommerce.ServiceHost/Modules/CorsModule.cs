using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.ServiceHost;

public sealed class CorsHostOptions
{
    /// <summary>اسم section في appsettings (افتراضيّ "Cors").</summary>
    public string SectionName { get; set; } = "Cors";

    /// <summary>هل يسمح بالـ credentials (cookies، Authorization). افتراضيّ true.</summary>
    public bool AllowCredentials { get; set; } = true;
}

public static class CorsModule
{
    /// <summary>
    /// يُسجِّل CORS من appsettings:
    /// <code>
    /// "Cors": {
    ///   "AllowedOrigins":         [ "https://example.com" ],
    ///   "AllowedOriginPatterns":  [ "https://*.runasp.net" ]
    /// }
    /// </code>
    /// </summary>
    public static ServiceHostBuilder UseCors(
        this ServiceHostBuilder host,
        Action<CorsHostOptions>? configure = null)
    {
        var opts = new CorsHostOptions();
        configure?.Invoke(opts);

        var section  = host.Builder.Configuration.GetSection(opts.SectionName);
        var origins  = section.GetSection("AllowedOrigins").Get<string[]>()  ?? Array.Empty<string>();
        var patterns = section.GetSection("AllowedOriginPatterns").Get<string[]>() ?? Array.Empty<string>();

        host.ConfigureApp(app =>
        {
            app.UseCors(b =>
            {
                if (origins.Length > 0)  b.WithOrigins(origins);
                if (patterns.Length > 0) b.SetIsOriginAllowedToAllowWildcardSubdomains().WithOrigins(patterns);
                b.AllowAnyHeader().AllowAnyMethod();
                if (opts.AllowCredentials) b.AllowCredentials();
            });
        });
        return host;
    }
}
