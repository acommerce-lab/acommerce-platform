using ACommerce.OperationEngine.Interceptors;
using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.Compositions.Customer.Chat.Realtime;

public static class ChatRealtimeExtensions
{
    /// <summary>
    /// يُسَجِّل ingestor الرَسائل الوارِدَة — مَدخَل push-from-hub. آمِن
    /// لِأيّ تَطبيق يَستَقبِل realtime، لا يَحتاج broadcaster.
    /// </summary>
    public static IServiceCollection AddChatRealtimeIngestor(this IServiceCollection services)
    {
        services.AddScoped<ChatRealtimeIngestor>();
        return services;
    }

    /// <summary>
    /// يُسَجِّل interceptor الـ broadcast — يَتَطَلَّب أن يَكون التَطبيق
    /// قَد سَجَّل <see cref="IChatRealtimeBroadcaster"/>. تَطبيقات تُريد
    /// بَثّ مِن العَميل مُباشَرَة (peer-to-peer أَو client-managed hub)
    /// تَستَخدِمه؛ التَطبيقات التي يَفعَل سيرفَرها الـ broadcast (مَثَل
    /// SignalR Hub server-driven) لا تَحتاجه.
    /// </summary>
    public static IServiceCollection AddChatRealtimeBroadcaster(this IServiceCollection services)
    {
        services.AddScoped<IOperationInterceptor, ChatRealtimeBroadcastInterceptor>();
        return services;
    }

    /// <summary>كِلاهُما — نَمَط P2P كامِل.</summary>
    public static IServiceCollection AddChatRealtimeComposition(this IServiceCollection services)
        => services.AddChatRealtimeIngestor().AddChatRealtimeBroadcaster();
}
