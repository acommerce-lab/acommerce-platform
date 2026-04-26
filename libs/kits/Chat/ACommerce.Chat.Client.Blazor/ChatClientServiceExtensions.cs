using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.Chat.Client.Blazor;

public static class ChatClientServiceExtensions
{
    /// <summary>
    /// يسجّل <see cref="IChatClient"/> كـ Scoped (لكلّ Blazor circuit) مع
    /// <see cref="ChatClientOptions"/> اختياريّة.
    /// </summary>
    public static IServiceCollection AddBlazorChatClient(
        this IServiceCollection services,
        Action<ChatClientOptions>? configure = null)
    {
        if (configure is not null) services.Configure(configure);
        else                       services.Configure<ChatClientOptions>(_ => { });

        services.AddScoped<IChatClient, ChatClient>();
        return services;
    }
}
