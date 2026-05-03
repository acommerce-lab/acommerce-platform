using ACommerce.ClientHost.Pages;
using ACommerce.Kits.Chat.Frontend.Customer.Pages;

namespace ACommerce.Kits.Chat.Frontend.Customer;

public sealed class ChatPageBundle : IPageBundle
{
    public string BundleId => "chat";

    public IEnumerable<KitPage> Pages =>
    [
        new("chat.inbox", typeof(AcChatInboxPage), "/chat",      RequiresAuth: true),
        new("chat.room",  typeof(AcChatRoomPage),  "/chat/{id}", RequiresAuth: true),
    ];
}
