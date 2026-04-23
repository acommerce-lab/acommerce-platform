namespace Order.Web.Store;

/// <summary>
/// Translation service interface — implement to swap translation providers
/// (e.g. EmbeddedTranslationProvider, ApiTranslationProvider, ResxTranslationProvider).
/// </summary>
public interface ITranslationProvider
{
    string Translate(string key, string language);
}

/// <summary>
/// Razor façade — delegates to <see cref="ITranslationProvider"/>.
/// Keep the same format (@inject L L) for pages: @(L["home.title"])
/// </summary>
public class L
{
    private readonly AppStore _store;
    private readonly ITranslationProvider _provider;

    public L(AppStore store, ITranslationProvider provider)
    {
        _store = store;
        _provider = provider;
    }

    public string this[string key] => _provider.Translate(key, _store.Ui.Language);

    public bool IsRtl => _store.Ui.IsRtl;
    public string Lang => _store.Ui.Language;
}

/// <summary>
/// Embedded translation dictionaries — replace later with ApiTranslationProvider
/// or ResxTranslationProvider by changing DI registration only.
/// </summary>
public sealed class EmbeddedTranslationProvider : ITranslationProvider
{
    public string Translate(string key, string language)
    {
        if (language == "en" && En.TryGetValue(key, out var en)) return en;
        return Ar.TryGetValue(key, out var ar) ? ar : key;
    }

    private static readonly Dictionary<string, string> Ar = new()
    {
        ["app.name"] = "اوردر",

        ["common.search"] = "بحث",
        ["common.cancel"] = "إلغاء",
        ["common.save"] = "حفظ",
        ["common.submit"] = "تأكيد",
        ["common.retry"] = "إعادة المحاولة",
        ["common.close"] = "إغلاق",
        ["common.loading"] = "جارٍ التحميل…",
        ["common.empty"] = "لا توجد عناصر",
        ["common.next"] = "التالي",
        ["common.prev"] = "السابق",
        ["common.confirm"] = "تأكيد",
        ["common.all"] = "الكل",
        ["common.saved"] = "تمّ الحفظ بنجاح",
        ["common.error"] = "حدث خطأ ما",

        ["nav.home"] = "الرئيسية",
        ["nav.search"] = "بحث",
        ["nav.orders"] = "طلباتي",
        ["nav.messages"] = "الرسائل",
        ["nav.cart"] = "السلة",
        ["nav.profile"] = "حسابي",
        ["nav.signin"] = "دخول",
        ["nav.brand"] = "اوردر",

        ["home.title"] = "اوردر",
        ["home.subtitle"] = "عروض اليوم من كافيهات ومطاعم المدينة",
        ["home.all"] = "الكل",
        ["home.loading"] = "جاري تحميل العروض…",
        ["home.empty"] = "لا توجد عروض",
        ["home.signin"] = "تسجيل الدخول",
        ["home.language_toggle"] = "English",

        ["settings.title"] = "الإعدادات",
        ["settings.theme"] = "المظهر",
        ["settings.theme.light"] = "فاتح",
        ["settings.theme.dark"] = "داكن",
        ["settings.language"] = "اللغة",
        ["settings.language.ar"] = "العربيّة",
        ["settings.language.en"] = "English",
        ["settings.about"] = "حول التطبيق",
        ["settings.version"] = "الإصدار",
        ["settings.sign_out"] = "تسجيل الخروج",
        ["settings.terms"] = "الشروط والأحكام",

        ["auth.login.title"] = "تسجيل الدخول - اوردر",
        ["auth.signin"] = "تسجيل الدخول",
        ["auth.guest"] = "متابعة كزائر",

        ["cart.title"] = "سلتي",
        ["cart.empty"] = "السلة فارغة",
        ["cart.checkout"] = "إكمال الدفع",

        ["orders.title"] = "طلباتي",
        ["orders.empty"] = "لا توجد طلبات",

        ["offer.details"] = "تفاصيل العرض",
        ["offer.add_to_cart"] = "أضف إلى السلة",

        ["legal.title"] = "الشروط والأحكام - اوردر",
        ["legal.terms"] = "الشروط والأحكام",
        ["legal.privacy"] = "سياسة الخصوصيّة",
    };

    private static readonly Dictionary<string, string> En = new()
    {
        ["app.name"] = "Order",

        ["common.search"] = "Search",
        ["common.cancel"] = "Cancel",
        ["common.save"] = "Save",
        ["common.submit"] = "Submit",
        ["common.retry"] = "Retry",
        ["common.close"] = "Close",
        ["common.loading"] = "Loading…",
        ["common.empty"] = "No items",
        ["common.next"] = "Next",
        ["common.prev"] = "Previous",
        ["common.confirm"] = "Confirm",
        ["common.all"] = "All",
        ["common.saved"] = "Saved successfully",
        ["common.error"] = "An error occurred",

        ["nav.home"] = "Home",
        ["nav.search"] = "Search",
        ["nav.orders"] = "Orders",
        ["nav.messages"] = "Messages",
        ["nav.cart"] = "Cart",
        ["nav.profile"] = "Profile",
        ["nav.signin"] = "Sign in",
        ["nav.brand"] = "Order",

        ["home.title"] = "Order",
        ["home.subtitle"] = "Today's deals from local cafes and restaurants",
        ["home.all"] = "All",
        ["home.loading"] = "Loading offers…",
        ["home.empty"] = "No offers",
        ["home.signin"] = "Sign in",
        ["home.language_toggle"] = "العربية",

        ["settings.title"] = "Settings",
        ["settings.theme"] = "Appearance",
        ["settings.theme.light"] = "Light",
        ["settings.theme.dark"] = "Dark",
        ["settings.language"] = "Language",
        ["settings.language.ar"] = "العربيّة",
        ["settings.language.en"] = "English",
        ["settings.about"] = "About",
        ["settings.version"] = "Version",
        ["settings.sign_out"] = "Sign out",
        ["settings.terms"] = "Terms & Conditions",

        ["auth.login.title"] = "Sign In - Order",
        ["auth.signin"] = "Sign in",
        ["auth.guest"] = "Continue as guest",

        ["cart.title"] = "My Cart",
        ["cart.empty"] = "Cart is empty",
        ["cart.checkout"] = "Checkout",

        ["orders.title"] = "My Orders",
        ["orders.empty"] = "No orders",

        ["offer.details"] = "Offer Details",
        ["offer.add_to_cart"] = "Add to Cart",

        ["legal.title"] = "Terms & Conditions - Order",
        ["legal.terms"] = "Terms & Conditions",
        ["legal.privacy"] = "Privacy Policy",
    };
}
