using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace ACommerce.ServiceHost;

public sealed class JwtAuthOptions
{
    public string Secret { get; set; } = "";
    public string Issuer { get; set; } = "";
    public string Audience { get; set; } = "";
    public TimeSpan ClockSkew { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// مسار realtime hub الذي يستلم التوكن في query <c>?access_token=…</c>
    /// (SignalR WebSocket لا يحمل Authorization header). افتراضيّ <c>/realtime</c>،
    /// أيّ تطبيق يضعه على مسار آخر يُمرّره.
    /// </summary>
    public string RealtimeHubPath { get; set; } = "/realtime";
}

public static class JwtAuthModule
{
    /// <summary>
    /// يُسجِّل JwtBearer scheme + Authorization. <c>MapInboundClaims=false</c>
    /// يُبقي <c>"user_id"</c> كما هو بدل ترجمته إلى <c>NameIdentifier</c>،
    /// فالتطبيقات تستهلكه عبر <c>User.FindFirst("user_id")</c> مباشرةً.
    /// </summary>
    public static ServiceHostBuilder UseJwtAuthentication(
        this ServiceHostBuilder host,
        Action<JwtAuthOptions> configure)
    {
        var opts = new JwtAuthOptions();
        configure(opts);
        if (string.IsNullOrEmpty(opts.Secret))
            throw new InvalidOperationException("JwtAuthOptions.Secret is required");

        host.Builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(o =>
            {
                o.MapInboundClaims = false;
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(opts.Secret)),
                    ValidateIssuer           = !string.IsNullOrEmpty(opts.Issuer),
                    ValidIssuer              = opts.Issuer,
                    ValidateAudience         = !string.IsNullOrEmpty(opts.Audience),
                    ValidAudience            = opts.Audience,
                    ValidateLifetime         = true,
                    ClockSkew                = opts.ClockSkew,
                };
                o.Events = new JwtBearerEvents
                {
                    OnMessageReceived = ctx =>
                    {
                        var token = ctx.Request.Query["access_token"];
                        if (!string.IsNullOrEmpty(token) &&
                            ctx.Request.Path.StartsWithSegments(opts.RealtimeHubPath))
                            ctx.Token = token;
                        return Task.CompletedTask;
                    }
                };
            });
        host.Builder.Services.AddAuthorization();

        // pipeline order: UseAuthentication() ثمّ UseAuthorization() قبل MapControllers
        host.ConfigureApp(app =>
        {
            app.UseAuthentication();
            app.UseAuthorization();
        });

        return host;
    }
}
