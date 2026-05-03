using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.Kits.Chat.Backend;

/// <summary>
/// سياسات Chat kit. <see cref="Authenticated"/> = أيّ مستخدم موثَّق
/// (التحقّق من المشاركة الفعليّة في المحادثة يَحدث في <c>IChatStore.CanParticipateAsync</c>).
/// التطبيق يُمكنه override بـ <c>RequireAssertion(_ =&gt; true)</c> في وضع التجربة.
/// </summary>
public static class ChatKitPolicies
{
    public const string Authenticated = "Chat.Authenticated";

    public static IServiceCollection AddChatKitPolicies(this IServiceCollection services)
    {
        services.AddAuthorization(opts =>
            opts.AddPolicy(Authenticated, p => p.RequireAuthenticatedUser()));
        return services;
    }
}
