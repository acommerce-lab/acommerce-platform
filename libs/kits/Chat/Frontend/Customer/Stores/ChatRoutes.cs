using ACommerce.Client.Http;
using ACommerce.ClientHost.Operations;
using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.Kits.Chat.Frontend.Customer.Stores;

/// <summary>
/// تَسجيل HTTP routes لِعَمَليات Chat kit. تُستَدعى مرّة واحدة عِند تَسجيل
/// التَطبيق لِـ <c>AddClientOpEngine</c> — HttpDispatcher يَستَخدِمها
/// لِتَحويل كلّ Operation إلى طَلَب HTTP صَحيح.
/// </summary>
public static class ChatRoutesExtensions
{
    public static IServiceCollection AddChatRoutes(this IServiceCollection services)
    {
        services.AddSingleton<IRoutesRegistrar, ChatRoutesRegistrar>();
        return services;
    }
}

internal sealed class ChatRoutesRegistrar : IRoutesRegistrar
{
    public void Register(HttpRouteRegistry routes)
    {
        routes.Map("chat.conversations.list", HttpMethod.Get,  "/conversations");
        routes.Map("chat.conversation.open",  HttpMethod.Get,  "/conversations/{id}");
        routes.Map("chat.enter",              HttpMethod.Post, "/chat/{id}/enter");
        routes.Map("chat.message.send",       HttpMethod.Post, "/conversations/{id}/messages");
    }
}
