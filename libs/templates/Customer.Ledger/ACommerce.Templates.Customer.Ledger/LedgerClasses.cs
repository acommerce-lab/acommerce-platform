namespace ACommerce.Templates.Customer.Ledger;

/// <summary>
/// أَسماء الكلاسات الإجباريّة التي يَكشِفها قالَب Customer Ledger. التَطبيقات
/// التي تُريد تَخصيص الألوان أو الخُطوط تَستبدِل CSS لِهذه الكلاسات في
/// <c>wwwroot/branding.css</c> دون لَمس القالَب.
///
/// <para>القاعدة: أيّ widget داخل القالَب يَجِب أن يَستهلِك كلاساً مِن هنا
/// بدلاً مِن string حُرّ. هذا يَضمَن أنّ التَطبيق يَملِك نُقطة override
/// واحِدة مَوثوقة.</para>
/// </summary>
public static class LedgerClasses
{
    public const string Shell           = "ledger-shell";
    public const string TopBar          = "ledger-topbar";
    public const string BottomNav       = "ledger-bottom-nav";

    public const string LoginCard       = "ledger-login-card";
    public const string LoginButton     = "ledger-login-btn";
    public const string LoginField      = "ledger-field";

    public const string ListingCard     = "ledger-listing-card";
    public const string ListingTitle    = "ledger-listing-title";
    public const string ListingPrice    = "ledger-listing-price";
    public const string ListingExplore  = "ledger-listing-explore";

    public const string ChatInbox       = "ledger-chat-inbox";
    public const string ChatRoom        = "ledger-chat-room";
    public const string ChatBubble      = "ledger-chat-bubble";

    public const string NotifInbox      = "ledger-notif-inbox";
    public const string ProfileCard     = "ledger-profile-card";
    public const string PlansGrid       = "ledger-plans-grid";
    public const string TicketsList     = "ledger-tickets-list";
    public const string FavoritesList   = "ledger-favorites-list";

    public const string Badge           = "ac-badge";
    public const string ButtonPrimary   = "oam-btn";
    public const string ButtonGhost     = "oam-btn-ghost";
    public const string ErrorText       = "ac-err";
}
