using Microsoft.AspNetCore.Builder;

namespace ACommerce.ServiceHost;

/// <summary>
/// واجهة chain لتركيب الخدمة الخلفيّة. كلّ أسطر <c>Use*</c> هي modules
/// مستقلّة قابلة للترك. التطبيق يبني ما يحتاج بـ chain صريح:
/// <code>
/// builder.AddACommerceServiceHost(host => host
///     .UseSerilog()
///     .UseOperationEngine()
///     .UseJwtAuthentication(jwt =&gt; { … })
///     .UseRealtime&lt;MyTransport, MyHub&gt;()
///     .UseCors()
///     .UseSwagger());
/// </code>
/// </summary>
public sealed class ServiceHostBuilder
{
    public WebApplicationBuilder Builder { get; }

    /// <summary>قائمة المسارات الخاصّة التي ينبغي تنفيذها قبل <c>app.Run()</c>.</summary>
    public List<Action<WebApplication>> AppConfigurators { get; } = new();

    /// <summary>قائمة hooks تنفَّذ عند الإقلاع داخل scope (هجرة DB، seeding، promotion).</summary>
    public List<Func<IServiceProvider, Task>> StartupHooks { get; } = new();

    public ServiceHostBuilder(WebApplicationBuilder builder)
    {
        Builder = builder;
    }

    /// <summary>سجِّل hook إقلاع — يُنفَّذ بعد <c>app.Build()</c> داخل scope جديد.</summary>
    public ServiceHostBuilder OnStartup(Func<IServiceProvider, Task> hook)
    {
        StartupHooks.Add(hook);
        return this;
    }

    /// <summary>سجِّل خطوة على الـ pipeline (middleware، endpoint، …) تنفَّذ عند <c>UseACommerceServiceHost</c>.</summary>
    public ServiceHostBuilder ConfigureApp(Action<WebApplication> configure)
    {
        AppConfigurators.Add(configure);
        return this;
    }
}
