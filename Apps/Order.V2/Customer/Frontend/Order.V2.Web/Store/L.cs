using ACommerce.L10n.Blazor;

namespace Order.V2.Web.Store;

/// <summary>
/// Razor façade — delegates to <see cref="ITranslationProvider"/>.
/// إبقاء نفس صيغة الاستخدام (<c>L["order.title"]</c>) للصفحات:
///   @inject L L
///   <h1>@(L["order.title"])</h1>
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
/// Implementation المضمّنة: قاموسين ثابتين. استبدلها لاحقاً بـ
/// <c>ApiTranslationProvider</c> أو <c>ResxTranslationProvider</c> بتغيير
/// تسجيل DI فقط.
/// </summary>
public sealed class CustomerTranslations : EmbeddedTranslationProvider
{
    protected override IReadOnlyDictionary<string, string> Ar => _ar;
    protected override IReadOnlyDictionary<string, string> En => _en;

    // ── القاموس العربيّ (الافتراضي) ──────────────────────────────────────
    private static readonly Dictionary<string, string> _ar = new()
    {
        ["app.name"] = "اوردر",
        ["app.tagline"] = "عروض الكافيهات والمطاعم",

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

        ["home.title"] = "اوردر — عروض اليوم",
        ["home.subtitle"] = "عروض اليوم من كافيهات ومطاعم المدينة",
        ["home.loading"] = "جاري تحميل العروض…",
        ["home.empty"] = "لا توجد عروض",

        ["nav.home"] = "الرئيسية",
        ["nav.search"] = "بحث",
        ["nav.favorites"] = "المفضلة",
        ["nav.cart"] = "السلة",
        ["nav.orders"] = "طلباتي",
        ["nav.messages"] = "المحادثات",
        ["nav.profile"] = "حسابي",
        ["nav.notifications"] = "الإشعارات",

        ["auth.login.title"] = "تسجيل الدخول",
        ["auth.welcome"] = "مرحباً بك",
        ["auth.phone"] = "رقم الجوال",
        ["auth.phone_subtitle"] = "أدخل رقم جوالك للمتابعة",
        ["auth.send_otp"] = "إرسال رمز التحقق",
        ["auth.sending"] = "جاري الإرسال...",
        ["auth.otp"] = "رمز التحقق (6 أرقام)",
        ["auth.otp_subtitle"] = "أدخل الرمز المرسل إلى",
        ["auth.verify"] = "تحقّق ودخول",
        ["auth.verifying"] = "جاري التحقق...",
        ["auth.change_phone"] = "تغيير رقم الجوال",
        ["auth.demo_mode"] = "وضع تجريبي:",
        ["auth.demo_accounts"] = "حسابات تجريبية:",
        ["auth.signed_out"] = "تمّ تسجيل الخروج",

        ["cart.title"] = "السلة",
        ["cart.page_title"] = "سلة الطلبات",
        ["cart.empty_title"] = "السلة فارغة",
        ["cart.empty_message"] = "تصفّح العروض وأضف أصنافك المفضلة",
        ["cart.browse"] = "تصفّح العروض",
        ["cart.pickup_hint"] = "الاستلام من المتجر أو من السيارة",
        ["cart.subtotal"] = "المجموع الفرعي",
        ["cart.total"] = "الإجمالي",
        ["cart.checkout"] = "متابعة للطلب",
        ["cart.clear"] = "إفراغ السلة",

        ["checkout.title"] = "إكمال الطلب",
        ["checkout.signin_required"] = "يجب تسجيل الدخول أولاً",
        ["checkout.signin"] = "تسجيل الدخول",
        ["checkout.empty_cart"] = "السلة فارغة",
        ["checkout.summary"] = "ملخص الطلب",
        ["checkout.total"] = "الإجمالي",
        ["checkout.pickup_method"] = "طريقة الاستلام",
        ["checkout.pickup_instore"] = "من المتجر",
        ["checkout.pickup_curbside"] = "من السيارة",
        ["checkout.car_details"] = "بيانات السيارة",
        ["checkout.car_model"] = "نوع السيارة",
        ["checkout.car_color"] = "اللون",
        ["checkout.car_plate"] = "رقم اللوحة (اختياري)",
        ["checkout.car_required"] = "أدخل بيانات السيارة",
        ["checkout.payment"] = "طريقة الدفع المفضلة",
        ["checkout.payment_hint"] = "الدفع يتم عند الاستلام في المتجر — لا دفع إلكتروني.",
        ["checkout.payment_cash"] = "نقدي",
        ["checkout.payment_card"] = "بطاقة",
        ["checkout.payment_amount"] = "المبلغ بالريال",
        ["checkout.payment_change"] = "الباقي المتوقع",
        ["checkout.payment_not_enough"] = "المبلغ غير كافٍ — يجب أن يساوي أو يتجاوز الإجمالي",
        ["checkout.payment_insufficient"] = "المبلغ المقدّم يجب أن يغطي الإجمالي",
        ["checkout.notes"] = "ملاحظات للتاجر (اختياري)",
        ["checkout.submit"] = "تأكيد الطلب",
        ["checkout.submitting"] = "جاري الإرسال...",
        ["checkout.back_to_cart"] = "العودة للسلة",
        ["checkout.error"] = "تعذّر إنشاء الطلب",

        ["favorites.title"] = "المفضلة",
        ["favorites.signin_required"] = "سجّل دخولك للحفظ",
        ["favorites.empty_title"] = "لا توجد عروض محفوظة",
        ["favorites.empty_message"] = "اضغط على القلب في صفحة العرض لحفظه",

        ["messages.title"] = "المحادثات",
        ["messages.signin_required"] = "سجّل دخولك لرؤية المحادثات",
        ["messages.empty_title"] = "لا محادثات بعد",
        ["messages.empty_message"] = "ابدأ من صفحة العرض بزر «محادثة المتجر»",
        ["messages.loading"] = "جاري التحميل...",
        ["messages.placeholder"] = "اكتب رسالة...",
        ["messages.send"] = "إرسال",

        ["orders.title"] = "طلباتي",
        ["orders.page_title"] = "طلباتي",
        ["orders.signin_required"] = "سجل دخولك لرؤية طلباتك",
        ["orders.my_orders"] = "طلباتي",
        ["orders.empty_title"] = "لا توجد طلبات",
        ["orders.empty_message"] = "اطلب أول وجبتك من اوردر",

        ["offer.details"] = "تفاصيل العرض",
        ["offer.price"] = "السعر",
        ["offer.description"] = "الوصف",
        ["offer.vendor"] = "التاجر",
        ["offer.rating"] = "التقييم",
        ["offer.not_found"] = "العرض غير موجود",
        ["offer.back_to_home"] = "العودة للرئيسية",
        ["offer.add_to_cart"] = "أضف إلى السلة",
        ["offer.chat_with_store"] = "محادثة المتجر",

        ["vendor.profile"] = "ملف التاجر",
        ["vendor.info"] = "معلومات المتجر",
        ["vendor.hours"] = "ساعات العمل",
        ["vendor.phone"] = "رقم الهاتف",
        ["vendor.location"] = "الموقع",
        ["vendor.offers"] = "العروض",

        ["search.title"] = "البحث",
        ["search.placeholder"] = "ابحث عن عرض…",

        ["settings.title"] = "الإعدادات",
        ["settings.language"] = "اللغة",
        ["settings.theme"] = "السمة",
        ["settings.theme_light"] = "فاتح",
        ["settings.theme_dark"] = "داكن",
        ["settings.sign_out"] = "تسجيل الخروج",

        ["profile.title"] = "حسابي",
        ["profile.edit"] = "تعديل الملف الشخصي",
        ["profile.account"] = "حسابي",

        ["legal.title"] = "المستندات القانونية",
        ["legal.privacy"] = "سياسة الخصوصية",
        ["legal.terms"] = "الشروط والأحكام",
        ["legal.updated"] = "آخر تحديث: أبريل 2026",
        ["legal.terms_header"] = "شروط الاستخدام",
        ["legal.refund_header"] = "سياسة الاسترجاع",

        ["design.catalog"] = "دليل التصاميم — اوردر",
        ["design.customer"] = "العميل: البرتقالي",

        ["conversation.title"] = "محادثة",
        ["conversation.signin_required"] = "سجل دخولك",
        ["conversation.loading"] = "جاري التحميل...",
        ["conversation.empty"] = "لا رسائل بعد",

        ["notifications.title"] = "الإشعارات",
        ["notifications.signin_required"] = "سجّل دخولك للإشعارات",
        ["notifications.empty_title"] = "لا إشعارات",
        ["notifications.empty_message"] = "سيظهر هنا كل جديد",
        ["notifications.mark_all_read"] = "تعليم الكل مقروءاً",

        ["demo.sara"] = "سارة",
    };

    // ── القاموس الإنجليزيّ ───────────────────────────────────────────────
    private static readonly Dictionary<string, string> _en = new()
    {
        ["app.name"] = "Order",
        ["app.tagline"] = "Cafe & restaurant deals",

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

        ["home.title"] = "Order — Today's offers",
        ["home.subtitle"] = "Today's deals from local cafes and restaurants",
        ["home.loading"] = "Loading offers…",
        ["home.empty"] = "No offers",

        ["nav.home"] = "Home",
        ["nav.search"] = "Search",
        ["nav.favorites"] = "Favorites",
        ["nav.cart"] = "Cart",
        ["nav.orders"] = "My orders",
        ["nav.messages"] = "Messages",
        ["nav.profile"] = "Account",
        ["nav.notifications"] = "Notifications",

        ["auth.login.title"] = "Sign In",
        ["auth.welcome"] = "Welcome",
        ["auth.phone"] = "Phone number",
        ["auth.phone_subtitle"] = "Enter your phone number to continue",
        ["auth.send_otp"] = "Send verification code",
        ["auth.sending"] = "Sending...",
        ["auth.otp"] = "Verification code (6 digits)",
        ["auth.otp_subtitle"] = "Enter the code sent to",
        ["auth.verify"] = "Verify & sign in",
        ["auth.verifying"] = "Verifying...",
        ["auth.change_phone"] = "Change phone",
        ["auth.demo_mode"] = "Demo mode:",
        ["auth.demo_accounts"] = "Demo accounts:",
        ["auth.signed_out"] = "Signed out",

        ["cart.title"] = "Cart",
        ["cart.page_title"] = "Your cart",
        ["cart.empty_title"] = "Cart is empty",
        ["cart.empty_message"] = "Browse offers and add your favorites",
        ["cart.browse"] = "Browse offers",
        ["cart.pickup_hint"] = "In-store or curbside pickup",
        ["cart.subtotal"] = "Subtotal",
        ["cart.total"] = "Total",
        ["cart.checkout"] = "Continue to checkout",
        ["cart.clear"] = "Clear cart",

        ["checkout.title"] = "Place order",
        ["checkout.signin_required"] = "Sign in first",
        ["checkout.signin"] = "Sign in",
        ["checkout.empty_cart"] = "Cart is empty",
        ["checkout.summary"] = "Order summary",
        ["checkout.total"] = "Total",
        ["checkout.pickup_method"] = "Pickup method",
        ["checkout.pickup_instore"] = "In-store",
        ["checkout.pickup_curbside"] = "Curbside",
        ["checkout.car_details"] = "Car details",
        ["checkout.car_model"] = "Model",
        ["checkout.car_color"] = "Color",
        ["checkout.car_plate"] = "Plate number (optional)",
        ["checkout.car_required"] = "Please enter car details",
        ["checkout.payment"] = "Preferred payment",
        ["checkout.payment_hint"] = "Payment happens at pickup — no online payment.",
        ["checkout.payment_cash"] = "Cash",
        ["checkout.payment_card"] = "Card",
        ["checkout.payment_amount"] = "Amount (SAR)",
        ["checkout.payment_change"] = "Expected change",
        ["checkout.payment_not_enough"] = "Amount must cover the total",
        ["checkout.payment_insufficient"] = "Cash amount must cover the total",
        ["checkout.notes"] = "Notes (optional)",
        ["checkout.submit"] = "Confirm order",
        ["checkout.submitting"] = "Sending…",
        ["checkout.back_to_cart"] = "Back to cart",
        ["checkout.error"] = "Could not create order",

        ["favorites.title"] = "Favorites",
        ["favorites.signin_required"] = "Sign in to save",
        ["favorites.empty_title"] = "No favorites yet",
        ["favorites.empty_message"] = "Tap the heart on any offer to save it",

        ["messages.title"] = "Messages",
        ["messages.signin_required"] = "Sign in to chat",
        ["messages.empty_title"] = "No conversations",
        ["messages.empty_message"] = "Tap \"Chat with store\" on any offer",
        ["messages.loading"] = "Loading…",
        ["messages.placeholder"] = "Type a message...",
        ["messages.send"] = "Send",

        ["orders.title"] = "Orders",
        ["orders.page_title"] = "My orders",
        ["orders.signin_required"] = "Sign in to view orders",
        ["orders.my_orders"] = "My orders",
        ["orders.empty_title"] = "No orders yet",
        ["orders.empty_message"] = "Place your first order",

        ["offer.details"] = "Offer details",
        ["offer.price"] = "Price",
        ["offer.description"] = "Description",
        ["offer.vendor"] = "Vendor",
        ["offer.rating"] = "Rating",
        ["offer.not_found"] = "Offer not found",
        ["offer.back_to_home"] = "Back to home",
        ["offer.add_to_cart"] = "Add to cart",
        ["offer.chat_with_store"] = "Chat with store",

        ["vendor.profile"] = "Vendor profile",
        ["vendor.info"] = "Store info",
        ["vendor.hours"] = "Open hours",
        ["vendor.phone"] = "Phone",
        ["vendor.location"] = "Location",
        ["vendor.offers"] = "Offers",

        ["search.title"] = "Search",
        ["search.placeholder"] = "Search for an offer…",

        ["settings.title"] = "Settings",
        ["settings.language"] = "Language",
        ["settings.theme"] = "Theme",
        ["settings.theme_light"] = "Light",
        ["settings.theme_dark"] = "Dark",
        ["settings.sign_out"] = "Sign out",

        ["profile.title"] = "Account",
        ["profile.edit"] = "Edit profile",
        ["profile.account"] = "Account",

        ["legal.title"] = "Legal documents",
        ["legal.privacy"] = "Privacy policy",
        ["legal.terms"] = "Terms and conditions",
        ["legal.updated"] = "Last updated: April 2026",
        ["legal.terms_header"] = "Terms of Use",
        ["legal.refund_header"] = "Refund Policy",

        ["design.catalog"] = "Design Catalog — Order",
        ["design.customer"] = "Customer: Orange",

        ["conversation.title"] = "Chat",
        ["conversation.signin_required"] = "Sign in",
        ["conversation.loading"] = "Loading…",
        ["conversation.empty"] = "No messages yet",

        ["notifications.title"] = "Notifications",
        ["notifications.signin_required"] = "Sign in for notifications",
        ["notifications.empty_title"] = "All caught up",
        ["notifications.empty_message"] = "New activity will appear here",
        ["notifications.mark_all_read"] = "Mark all read",

        ["demo.sara"] = "Sara",
    };
}
