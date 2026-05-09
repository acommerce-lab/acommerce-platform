using Serilog;

namespace ACommerce.ServiceHost;

public static class SerilogModule
{
    /// <summary>
    /// يُهيِّئ Serilog من <c>builder.Configuration</c>:
    /// <list type="bullet">
    ///   <item>Console + rolling file (<c>logs/{appName}-.log</c> يوميّاً).</item>
    ///   <item>أيّ <c>Serilog</c> section في appsettings تتعاون.</item>
    /// </list>
    /// </summary>
    public static ServiceHostBuilder UseSerilog(this ServiceHostBuilder host, string? appName = null)
    {
        var name = appName ?? host.Builder.Environment.ApplicationName;
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(host.Builder.Configuration)
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.File($"logs/{name}-.log", rollingInterval: RollingInterval.Day)
            .CreateLogger();
        host.Builder.Host.UseSerilog();
        return host;
    }
}
