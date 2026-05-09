using ACommerce.Realtime.Operations;
using ACommerce.Realtime.Operations.Abstractions;
using ACommerce.Realtime.Providers.InMemory;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.ServiceHost;

public static class RealtimeModule
{
    /// <summary>
    /// يُسجِّل SignalR + InMemory connection tracker + Realtime channels +
    /// transport التطبيق. <typeparamref name="TTransport"/> كـ Singleton لأنّه
    /// يستهلك <c>IHubContext&lt;THub&gt;</c> الذي مأمون.
    ///
    /// <para>التطبيق <i>يجب</i> أن يستدعي <c>app.MapHub&lt;THub&gt;("/realtime")</c>
    /// بنفسه — لا نُسجّله هنا لأنّ المسار قد يحوي options خاصّة بالتطبيق
    /// (Authorize، long polling، الخ.).</para>
    /// </summary>
    public static ServiceHostBuilder UseRealtime<TTransport, THub>(
        this ServiceHostBuilder host,
        IUserIdProvider? userIdProvider = null)
        where TTransport : class, IRealtimeTransport
        where THub : Hub
    {
        var s = host.Builder.Services;

        s.AddSignalR();
        s.AddSingleton<IRealtimeTransport, TTransport>();
        RealtimeExtensions.AddRealtimeChannels(s);

        s.AddSingleton<InMemoryConnectionTracker>();
        s.AddSingleton<IConnectionTracker>(sp => sp.GetRequiredService<InMemoryConnectionTracker>());

        if (userIdProvider is not null)
            s.AddSingleton(userIdProvider);

        return host;
    }
}
