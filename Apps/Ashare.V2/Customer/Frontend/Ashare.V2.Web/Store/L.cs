namespace Ashare.V2.Web.Store;

/// <summary>
/// Razor façade — delegates to <see cref="ITranslationProvider"/>.
/// إبقاء نفس صيغة الاستخدام (<c>L["home.title"]</c>) للصفحات:
///   @inject L L
///   <h1>@(L["home.title"])</h1>
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
public sealed class EmbeddedTranslationProvider : ITranslationProvider
{
    public string Translate(string key, string language)
    {
        if (language == "en" && En.TryGetValue(key, out var en)) return en;
        return Ar.TryGetValue(key, out var ar) ? ar : key;
    }

    // ── القاموس العربيّ (الافتراضي) ──────────────────────────────────────
    private static readonly Dictionary<string, string> Ar = new()
    {
        ["app.name"] = "عشير",
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

        ["home.title"] = "ابحث عن سكنك المشترك",
        ["home.subtitle"] = "تطبيق السكن المشترك الأول في السعودية",
        ["home.search.placeholder"] = "ابحث عن سكن مشترك…",
        ["home.categories.title"] = "الفئات",
        ["home.featured.title"] = "سكن مميّز",
        ["home.new.title"] = "أُضيف حديثاً",
        ["home.cta.title"] = "لديك غرفة أو شقة للمشاركة؟",
        ["home.cta.subtitle"] = "أضف سكنك الآن وابدأ بالكسب من مشاركته",
        ["home.view_all"] = "عرض الكل",
        ["home.city"] = "المدينة",

        ["nav.home"] = "الرئيسية",
        ["nav.explore"] = "استكشف",
        ["nav.favorites"] = "المفضلة",
        ["nav.bookings"] = "حجوزاتي",
        ["nav.profile"] = "حسابي",
        ["nav.notifications"] = "الإشعارات",

        ["auth.login.title"] = "تسجيل الدخول",
        ["auth.nafath.continue"] = "المتابعة بنفاذ",
        ["auth.guest.continue"] = "المتابعة كزائر",
        ["auth.national_id"] = "رقم الهويّة الوطنيّة",
        ["auth.phone"] = "رقم الجوّال",
        ["auth.otp"] = "رمز التحقق",
        ["auth.send_otp"] = "أرسل الرمز",
        ["auth.verify"] = "تحقّق",
        ["auth.signed_out"] = "تمّ تسجيل الخروج",
        ["auth.or"] = "أو",
        ["auth.nafath.select_number"] = "افتح تطبيق نفاذ واختر الرقم التالي",
        ["auth.nafath.success"] = "تمّ تسجيل الدخول بنجاح",
        ["auth.nafath.failed"] = "فشل التحقّق",
        ["auth.nafath.expired"] = "انتهت الجلسة — حاول مجدّداً",
        ["auth.nafath.retry"] = "إعادة المحاولة",
        ["auth.nafath.cancel"] = "إلغاء",
        ["auth.nafath.seconds"] = "ثانية",

        ["booking.start"] = "احجز الآن",
        ["booking.nights"] = "عدد الليالي",
        ["booking.capacity"] = "عدد الأشخاص",
        ["booking.total"] = "المجموع",
        ["booking.confirm"] = "تأكيد الحجز",
        ["booking.cancel"] = "إلغاء الحجز",
        ["booking.state.pending"] = "قيد المراجعة",
        ["booking.state.confirmed"] = "مؤكَّد",
        ["booking.state.completed"] = "منتهٍ",
        ["booking.state.cancelled"] = "ملغى",
        ["booking.state.confirmed_short"] = "مؤكَّدة",
        ["booking.state.completed_short"] = "منتهية",
        ["booking.state.cancelled_short"] = "ملغاة",
        ["booking.empty_state"] = "لا توجد حجوزات",

        ["listing.amenities"] = "المرافق",
        ["listing.description"] = "الوصف",
        ["listing.owner"] = "المالك",
        ["listing.no_results"] = "لا توجد نتائج",
        ["listing.create.title"] = "أضف إعلانك",
        ["listing.create.field.title"] = "العنوان",
        ["listing.create.field.price"] = "السعر",
        ["listing.create.field.price_sar"] = "السعر (SAR)",
        ["listing.create.field.city"] = "المدينة",
        ["listing.create.field.district"] = "الحيّ / المنطقة",
        ["listing.create.field.category"] = "الفئة",
        ["listing.create.field.capacity"] = "السعة (عدد الأشخاص)",
        ["listing.create.field.amenities"] = "المرافق المتوفّرة",
        ["listing.create.field.time_unit"] = "الوحدة الزمنيّة",
        ["listing.create.field.latitude"] = "خط العرض (اختياريّ)",
        ["listing.create.field.longitude"] = "خط الطول (اختياريّ)",

        ["listing.step.category"] = "الفئة",
        ["listing.step.details"] = "التفاصيل",
        ["listing.step.images"] = "الصور",
        ["listing.step.location"] = "الموقع",
        ["listing.choose_category"] = "اختر فئة الإعلان",
        ["listing.listing_details"] = "تفاصيل الإعلان",
        ["listing.listing_images"] = "صور الإعلان",
        ["listing.listing_location"] = "موقع الإعلان",
        ["listing.images_help"] = "حتّى @max صور • كل صورة أقصى 5MB • الصورة الأولى هي الرئيسيّة.",
        ["listing.add_images"] = "إضافة صور",
        ["listing.main_image"] = "رئيسيّة",
        ["listing.make_main"] = "اجعلها رئيسيّة",
        ["listing.delete"] = "حذف",
        ["listing.back"] = "رجوع",
        ["listing.next"] = "التالي",
        ["listing.publish"] = "نشر الإعلان",
        ["listing.title_placeholder"] = "مثلاً: شقة مفروشة في حيّ النرجس",
        ["listing.description_placeholder"] = "اذكر المساحة، الغرف، الميزات…",
        ["listing.district_placeholder"] = "مثلاً: النرجس",
        ["listing.map_location"] = "الموقع",
        ["listing.time_unit.day"] = "يوميّ",
        ["listing.time_unit.month"] = "شهريّ",
        ["listing.time_unit.year"] = "سنويّ",

        ["payment.title"] = "بيانات الدفع",
        ["payment.card_number"] = "رقم البطاقة",
        ["payment.expiry"] = "تاريخ الانتهاء",
        ["payment.cvv"] = "CVV",
        ["payment.holder"] = "اسم حامل البطاقة",
        ["payment.pay"] = "ادفع",
        ["payment.success"] = "تمّ الدفع بنجاح",
        ["payment.failed"] = "فشل الدفع",

        ["settings.title"] = "الإعدادات",
        ["settings.theme"] = "السمة",
        ["settings.theme.light"] = "فاتح",
        ["settings.theme.dark"] = "داكن",
        ["settings.language"] = "اللغة",
        ["settings.sign_out"] = "تسجيل الخروج",

        ["legal.privacy"] = "سياسة الخصوصيّة",
        ["legal.terms"] = "الشروط والأحكام",
        ["legal.return"] = "سياسة الاسترداد",

        ["version.required"] = "تحديث مطلوب",
        ["version.required_body"] = "يجب تحديث التطبيق للاستمرار في استخدامه",
        ["version.download"] = "تنزيل الإصدار الجديد",
        ["version.current"] = "الإصدار الحالي",
        ["version.latest"] = "الإصدار المتاح",

        ["complaint.title"] = "الشكاوى",
        ["complaint.new"] = "شكوى جديدة",
        ["complaint.subject"] = "الموضوع",
        ["complaint.body"] = "التفاصيل",
        ["complaint.send"] = "إرسال",
        ["complaint.page_title"] = "الشكاوى والملاحظات",
        ["complaint.priority"] = "الأولويّة",
        ["complaint.related"] = "مرتبطة بـ (اختياريّ)",
        ["complaint.related_placeholder"] = "الحجز #B-1 أو الإعلان L-101",
        ["complaint.priority.normal"] = "عادي",
        ["complaint.priority.high"] = "عالي",
        ["complaint.priority.urgent"] = "عاجل",
        ["complaint.description"] = "اشرح المشكلة بالتفصيل…",
        ["complaint.cancel"] = "إلغاء",
        ["complaint.status.open"] = "مفتوحة",
        ["complaint.status.in_progress"] = "قيد المعالجة",
        ["complaint.status.resolved"] = "محلولة",
        ["complaint.status.closed"] = "مغلقة",

        ["subscription.title"] = "اشتراكي",
        ["subscription.days_remaining"] = "يوم متبقّي",
        ["subscription.renew"] = "تجديد",
        ["subscription.change"] = "تغيير الخطّة",
        ["subscription.cancel"] = "إلغاء الاشتراك",
        ["subscription.usage"] = "استهلاك الكوتا",
        ["subscription.features"] = "مزايا الخطّة",
        ["subscription.invoices"] = "الفواتير",

        ["profile.edit"] = "تعديل الملفّ الشخصيّ",
        ["profile.avatar"] = "الصورة الشخصيّة",
        ["profile.verified"] = "موثَّق",
        ["profile.verify"] = "تحقّق",

        ["quickaction.find_roommate"] = "ابحث عن شريك سكن",
        ["quickaction.my_requests"] = "طلباتي",

        ["auth.nafath"] = "نفاذ",
        ["auth.national_id_placeholder"] = "10 أرقام",

        ["settings.legal_docs"] = "المستندات القانونيّة",
        ["settings.language.ar"] = "العربيّة",
        ["settings.language.en"] = "English"
    };

    // ── القاموس الإنجليزيّ ───────────────────────────────────────────────
    private static readonly Dictionary<string, string> En = new()
    {
        ["app.name"] = "Ashare",
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

        ["home.title"] = "Find your shared home",
        ["home.subtitle"] = "Saudi Arabia's first shared-housing app",
        ["home.search.placeholder"] = "Search for shared housing…",
        ["home.categories.title"] = "Categories",
        ["home.featured.title"] = "Featured",
        ["home.new.title"] = "New",
        ["home.cta.title"] = "Have a room or apartment to share?",
        ["home.cta.subtitle"] = "List your space and start earning",
        ["home.view_all"] = "View all",
        ["home.city"] = "City",

        ["nav.home"] = "Home",
        ["nav.explore"] = "Explore",
        ["nav.favorites"] = "Favorites",
        ["nav.bookings"] = "Bookings",
        ["nav.profile"] = "Profile",
        ["nav.notifications"] = "Notifications",

        ["auth.login.title"] = "Sign In",
        ["auth.nafath.continue"] = "Continue with Nafath",
        ["auth.guest.continue"] = "Continue as guest",
        ["auth.national_id"] = "National ID",
        ["auth.phone"] = "Phone number",
        ["auth.otp"] = "Verification code",
        ["auth.send_otp"] = "Send code",
        ["auth.verify"] = "Verify",
        ["auth.signed_out"] = "Signed out",
        ["auth.or"] = "Or",
        ["auth.nafath.select_number"] = "Open Nafath and select the number below",
        ["auth.nafath.success"] = "Signed in successfully",
        ["auth.nafath.failed"] = "Verification failed",
        ["auth.nafath.expired"] = "Session expired — try again",
        ["auth.nafath.retry"] = "Try again",
        ["auth.nafath.cancel"] = "Cancel",
        ["auth.nafath.seconds"] = "seconds",

        ["booking.start"] = "Book now",
        ["booking.nights"] = "Nights",
        ["booking.capacity"] = "Capacity",
        ["booking.total"] = "Total",
        ["booking.confirm"] = "Confirm booking",
        ["booking.cancel"] = "Cancel booking",
        ["booking.state.pending"] = "Pending",
        ["booking.state.confirmed"] = "Confirmed",
        ["booking.state.completed"] = "Completed",
        ["booking.state.cancelled"] = "Cancelled",
        ["booking.state.confirmed_short"] = "Confirmed",
        ["booking.state.completed_short"] = "Completed",
        ["booking.state.cancelled_short"] = "Cancelled",
        ["booking.empty_state"] = "No bookings",

        ["listing.amenities"] = "Amenities",
        ["listing.description"] = "Description",
        ["listing.owner"] = "Owner",
        ["listing.no_results"] = "No results",
        ["listing.create.title"] = "Add your listing",
        ["listing.create.field.title"] = "Title",
        ["listing.create.field.price"] = "Price",
        ["listing.create.field.price_sar"] = "Price (SAR)",
        ["listing.create.field.city"] = "City",
        ["listing.create.field.district"] = "District / Area",
        ["listing.create.field.category"] = "Category",
        ["listing.create.field.capacity"] = "Capacity (number of people)",
        ["listing.create.field.amenities"] = "Available amenities",
        ["listing.create.field.time_unit"] = "Time unit",
        ["listing.create.field.latitude"] = "Latitude (optional)",
        ["listing.create.field.longitude"] = "Longitude (optional)",

        ["listing.step.category"] = "Category",
        ["listing.step.details"] = "Details",
        ["listing.step.images"] = "Images",
        ["listing.step.location"] = "Location",
        ["listing.choose_category"] = "Choose listing category",
        ["listing.listing_details"] = "Listing details",
        ["listing.listing_images"] = "Listing images",
        ["listing.listing_location"] = "Listing location",
        ["listing.images_help"] = "Up to @max images • Max 5MB each • First image is main",
        ["listing.add_images"] = "Add images",
        ["listing.main_image"] = "Main",
        ["listing.make_main"] = "Make main",
        ["listing.delete"] = "Delete",
        ["listing.back"] = "Back",
        ["listing.next"] = "Next",
        ["listing.publish"] = "Publish listing",
        ["listing.title_placeholder"] = "E.g.: Furnished apartment in Al-Nakheel",
        ["listing.description_placeholder"] = "Mention area, rooms, features…",
        ["listing.district_placeholder"] = "E.g.: Al-Nakheel",
        ["listing.map_location"] = "Location",
        ["listing.time_unit.day"] = "Daily",
        ["listing.time_unit.month"] = "Monthly",
        ["listing.time_unit.year"] = "Yearly",

        ["payment.title"] = "Payment details",
        ["payment.card_number"] = "Card number",
        ["payment.expiry"] = "Expiry",
        ["payment.cvv"] = "CVV",
        ["payment.holder"] = "Cardholder name",
        ["payment.pay"] = "Pay",
        ["payment.success"] = "Payment successful",
        ["payment.failed"] = "Payment failed",

        ["settings.title"] = "Settings",
        ["settings.theme"] = "Theme",
        ["settings.theme.light"] = "Light",
        ["settings.theme.dark"] = "Dark",
        ["settings.language"] = "Language",
        ["settings.sign_out"] = "Sign out",

        ["legal.privacy"] = "Privacy policy",
        ["legal.terms"] = "Terms of service",
        ["legal.return"] = "Refund policy",

        ["version.required"] = "Update required",
        ["version.required_body"] = "Please update the app to continue using it",
        ["version.download"] = "Download latest version",
        ["version.current"] = "Current version",
        ["version.latest"] = "Latest version",

        ["complaint.title"] = "Complaints",
        ["complaint.new"] = "New complaint",
        ["complaint.subject"] = "Subject",
        ["complaint.body"] = "Details",
        ["complaint.send"] = "Send",
        ["complaint.page_title"] = "Complaints and feedback",
        ["complaint.priority"] = "Priority",
        ["complaint.related"] = "Related to (optional)",
        ["complaint.related_placeholder"] = "Booking #B-1 or Listing L-101",
        ["complaint.priority.normal"] = "Normal",
        ["complaint.priority.high"] = "High",
        ["complaint.priority.urgent"] = "Urgent",
        ["complaint.description"] = "Describe the issue in detail…",
        ["complaint.cancel"] = "Cancel",
        ["complaint.status.open"] = "Open",
        ["complaint.status.in_progress"] = "In progress",
        ["complaint.status.resolved"] = "Resolved",
        ["complaint.status.closed"] = "Closed",

        ["subscription.title"] = "My Subscription",
        ["subscription.days_remaining"] = "days remaining",
        ["subscription.renew"] = "Renew",
        ["subscription.change"] = "Change plan",
        ["subscription.cancel"] = "Cancel subscription",
        ["subscription.usage"] = "Usage",
        ["subscription.features"] = "Plan features",
        ["subscription.invoices"] = "Invoices",

        ["profile.edit"] = "Edit profile",
        ["profile.avatar"] = "Avatar",
        ["profile.verified"] = "Verified",
        ["profile.verify"] = "Verify",

        ["quickaction.find_roommate"] = "Find a roommate",
        ["quickaction.my_requests"] = "My requests",

        ["auth.nafath"] = "Nafath",
        ["auth.national_id_placeholder"] = "10 digits",

        ["settings.legal_docs"] = "Legal documents",
        ["settings.language.ar"] = "العربيّة",
        ["settings.language.en"] = "English"
    };
}
