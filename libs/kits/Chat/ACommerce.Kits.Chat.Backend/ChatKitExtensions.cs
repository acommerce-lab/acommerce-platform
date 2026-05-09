using ACommerce.Chat.Operations;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.Kits.Chat.Backend;

public static class ChatKitExtensions
{
    /// <summary>
    /// يسجّل Chat Kit بالكامل: <see cref="IChatStore"/> ينحلّ إلى <typeparamref name="TStore"/>،
    /// خيارات <see cref="ChatKitOptions"/>، خدمة الدردشة المجرّدة <see cref="IChatService"/>،
    /// و <see cref="ChatController"/> يُكتشف عبر <c>AddApplicationPart</c>.
    ///
    /// <para>الاستخدام في <c>Program.cs</c>:</para>
    /// <code>
    /// builder.Services.AddChatKit&lt;EjarChatStore&gt;(opts =&gt;
    /// {
    ///     opts.PartyKind = "Provider"; // أو "Admin" / "User"
    /// });
    /// </code>
    /// </summary>
    public static IServiceCollection AddChatKit<TStore>(
        this IServiceCollection services,
        Action<ChatKitOptions>? configure = null)
        where TStore : class, IChatStore
    {
        var options = new ChatKitOptions();
        configure?.Invoke(options);
        services.AddSingleton(options);

        services.AddSingleton<IChatStore, TStore>();

        // IChatService is provided by ACommerce.Chat.Operations; AddChat() must be
        // called separately (or rely on it already being registered).
        services.AddChat();

        // Make the controller discoverable when the host clears + filters
        // ApplicationParts (common in our backends).
        services.AddControllers()
            .AddApplicationPart(typeof(ChatController).Assembly);
        services.AddChatKitPolicies();

        return services;
    }
}
