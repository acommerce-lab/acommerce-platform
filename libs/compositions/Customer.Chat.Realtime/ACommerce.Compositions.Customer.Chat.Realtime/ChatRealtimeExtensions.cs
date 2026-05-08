using ACommerce.OperationEngine.Interceptors;
using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.Compositions.Customer.Chat.Realtime;

public static class ChatRealtimeExtensions
{
    /// <summary>
    /// يُسَجِّل composition Chat-Realtime — interceptor البَثّ + ingestor
    /// الرَسائل الوارِدَة. التَطبيق يَجِب أن يُسَجِّل تَنفيذ
    /// <see cref="IChatRealtimeBroadcaster"/> (عَبر SignalR / WebSocket).
    /// </summary>
    public static IServiceCollection AddChatRealtimeComposition(this IServiceCollection services)
    {
        services.AddScoped<IOperationInterceptor, ChatRealtimeBroadcastInterceptor>();
        services.AddScoped<ChatRealtimeIngestor>();
        return services;
    }
}
