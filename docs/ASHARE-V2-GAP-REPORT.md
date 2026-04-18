# Ashare V2 — تقرير فجوات الترحيل (مقارنة كاملة)

**المصدر القديم**: `/tmp/ACommerce.Libraries/Apps/Ashare.Shared/Components/Pages/` (25 صفحة، 4,148 سطراً)
**الوجهة V2**: `Apps/Ashare.V2/Customer/Frontend/Ashare.V2.Web/Components/Pages/` (20 صفحة، 1,356 سطراً)

**الخلاصة**: V2 فيها انكماش 68% بالأسطر — ليس بسبب التبسيط، بل لأنّ:
1. منطق كثير مخفيّ داخل قوالب (`AcBookingWizardPage`, `AcPaymentCheckoutPage`, `AcCreateListingPage`) بدون تعريض خصائصه.
2. 11 صفحة إمّا مفقودة أو مُبتورة لشظيّة فقط.
3. صفحات "موجودة" (Search/Explore/Notifications) تفتقد فلاتر وميزات إدارة.

---

## خريطة 1:1 — OLD vs V2

| # | OLD | أسطر | V2 | أسطر | الحالة | السبب |
|---|---|---|---|---|---|---|
| 1 | Auth/Login | 667 | Login | 155 | ⚠️ | Nafath مستعاد، لكن SignalR polling + MAUI lifecycle مفقودان |
| 2 | Auth/ProfileEdit | 382 | — | 0 | ❌ | مفقودة كلّياً — لا تحرير بروفايل، لا رفع صورة، لا تحقّق جوّال |
| 3 | Auth/Register | 16 | — | 0 | ✅ | القديم يُحوّل للّوجن — V2 تفعل المثل ضمناً |
| 4 | Home | 234 | Home | 126 | ⚠️ | CTA مبسّطة، quick actions مُختصَرة |
| 5 | Explore | 467 | Explore | 124 | ❌ | خريطة مبتورة، فلاتر ناقصة، sort/view toggle مفقودان |
| 6 | Search | 349 | Search | 82 | ❌ | recent searches + popular tags + quick filters مفقودة |
| 7 | SpaceDetails | 1455 | SpaceDetails | 85 | ❌ | gallery/map/amenities/owner card كلّها مُختبِئة في قوالب |
| 8 | Favorites | 123 | Favorites | 71 | ⚠️ | منطق موجود، guest-mode بسيط |
| 9 | BookingCreate | 965 | NewBooking | 40 | ❌ | **حرج** — date picker، حساب وديعة، payment sim، terms checkbox كلّها مفقودة من الصفحة |
| 10 | Bookings | 413 | Bookings | 65 | ❌ | فلتر بالحالة، ترتيب، timeline view مفقودة |
| 11 | BookingDetails | 988 | BookingDetails | 78 | ⚠️ | زرّ إلغاء مشروط (مُصلَح)، لكن host actions/protection info/refund مخفيّة |
| 12 | CreateListing | 585 | CreateListing | 42 | ❌ | 4 خطوات (category→details→images→location) مبتورة لنموذج واحد بلا step indicator |
| 13 | Chat/Chats | 227 | Chats | 43 | ❌ | search + unread badge + last-message preview مفقودة |
| 14 | Chat/ChatRoom | 455 | ChatRoom | 62 | ⚠️ | real-time، typing، attachments — معظمها مفقود في V2 |
| 15 | Complaints | 759 | Help | 30 | ❌ | Help هي صفحة إجراءات فقط — لا قائمة شكاوى، لا نموذج إنشاء |
| 16 | ComplaintDetails | 726 | — | 0 | ❌ | مفقودة كلّياً — لا thread، لا reply input، لا IsStaff badge |
| 17 | Notifications | 170 | Notifications | 59 | ⚠️ | filter-by-type، mark-as-read، تجميع بالتاريخ مفقودة |
| 18 | Profile | 483 | Me | 50 | ❌ | بطاقة بروفايل فقط + 4 أزرار تنقّل — لا stats، لا quick settings |
| 19 | (جزء Profile) | — | Settings | 52 | ❌ | theme/language موجودة، لكن notification prefs، privacy مفقودة |
| 20 | Language | 165 | (ضمن Settings/TopNav) | — | ✅ | مدمجة |
| 21 | LegalPageView | 220 | Legal | 44 | ⚠️ | markdown قد لا يُرَنْدَر، accordions مفقودة |
| 22 | Host/MySpaces | 599 | MyListings | 42 | ❌ | لا edit/publish/deactivate per card، لا stats، لا فلتر |
| 23 | Host/SubscriptionPlans | 296 | Plans | 44 | ❌ | لا جدول مقارنة، لا annual/quarterly، لا feature highlights |
| 24 | Host/SubscriptionCheckout | 828 | Subscribe | 43 | ❌ | **حرج** — Moyasar/Tap fields، VAT، billing cycle، promo code كلّها داخل القالب فقط |
| 25 | Host/SubscriptionDashboard | 352 | — | 0 | ❌ | MySubscription مفقودة — لا usage quotas، لا invoices، لا renewal date |
| 26 | Host/PaymentCallback | 326 | PaymentCallback | 27 | ⚠️ | تحويل فقط، لا payment confirmation، لا invoice display |

**الخلاصة**: ✅ 4 | ⚠️ 10 | ❌ 12

---

## الفجوات الملموسة (مرتّبة حسب الخطورة)

### ❌ مفقود كلّياً (6)

1. **Auth/ProfileEdit** — بروفايل المستخدم لا يمكن تعديله. لا avatar upload، لا phone verification.
2. **ComplaintDetails** — لا thread، المستخدم لا يرى ردّ الدعم.
3. **Host/SubscriptionDashboard (MySubscription)** — المستخدم لا يرى اشتراكه النشط، الكوتا، الفواتير.
4. **Complaint creation form** — Help صفحة stub.
5. **Chats search + unread badge** — قائمة المحادثات بدون مزايا إدارة.
6. **Booking wizard true steps** — NewBooking صفحة تمرّر params لقالب، بلا step indicator أو preview.

### ⚠️ مبتور جزئياً (10)

1. **Login** — Nafath موجود لكن بلا SignalR polling fallback.
2. **SpaceDetails** — كلّ الـ UI في قالب، الصفحة فارغة 85 سطراً.
3. **BookingDetails** — زرّ الإلغاء مشروط (مُصلَح)، لكن لا host actions.
4. **ChatRoom** — SignalR موجود، attachments مفقودة، typing مُبسَّط.
5. **Notifications** — قائمة فقط، لا filter/mark-read.
6. **Explore** — خريطة Leaflet غير مفعّلة، فلاتر متعدّدة مفقودة.
7. **Search** — recent searches + popular tags مفقودة.
8. **PaymentCallback** — redirect فقط، لا تأكيد/فاتورة.
9. **Legal** — markdown بسيط، لا accordions.
10. **Plans** — بطاقات فقط، لا جدول مقارنة.

---

## جذر المشكلة

1. **إفراط تجريدي**: `AcBookingWizardPage`, `AcPaymentCheckoutPage`, `AcCreateListingPage` تبتلع المنطق بدون تعريض params كافية.
2. **بناء component-first قبل أن نعرف ما الصفحة بحاجة إليه**.
3. **انكماش أسطر ≠ تبسيط** — الميزات مدفونة لا مُلغاة (بعضها غير موجود).
4. **غياب اختبارات ندّية**: لا توجد فحوص "هل كل ميزة قديمة لها مقابل في V2".

---

## أولويّات الترحيل الحقيقيّ (تدفق عمل مقترح)

**دفعة أ — مسدود المسار (يجب قبل أيّ شيء):**
1. `ProfileEdit` — بدونها، المستخدم الجديد يُحوَّل من Nafath إلى صفحة غير موجودة.
2. `SubscriptionDashboard (MySubscription)` — بدونها، لا معنى لأزرار "اشتراكي" في Me.
3. `Complaints` + `ComplaintDetails` — Help stub غير مفيد.

**دفعة ب — الميزات الحرجة:**
4. `BookingCreate` — إعادة بناء 4 خطوات (date, capacity, confirm, payment sim) في الصفحة نفسها لا في قالب مدفون.
5. `CreateListing` — إعادة بناء 4 خطوات (category→details→images→location) + step indicator + amenity chips + upload UI.
6. `SubscriptionCheckout` — إعادة تعريض VAT، billing cycle، promo code على الصفحة.

**دفعة ج — تحسين UX:**
7. `Search` — recent/popular/quick filters.
8. `Explore` — إعادة تفعيل Leaflet + amenity multi-select.
9. `SpaceDetails` — إظهار gallery/map/amenities/owner في الصفحة مباشرة.
10. `Chats/ChatRoom` — search + unread + attachments UI.
11. `Notifications` — filters + mark-read.
12. `MySpaces` — per-card actions + stats.
13. `Plans` — جدول مقارنة.
14. `Profile/Me` — stats + quick settings.
15. `PaymentCallback` — تأكيد + فاتورة.

---

## ما أُنجِز فعلاً في هذه الدفعة (الإصلاحات الأوّلية)

1. ✅ **Nafath مُستَعاد** في Login: دائرة رقم عشوائيّ + عدّاد 120 ثانية + حالات (loading/success/failed/expired/cancel).
2. ✅ **التبديل الحيّ للوضع الداكن والاتجاه**: JS interop في `ui-prefs.js` يُستدعى من `MainLayout.OnAfterRenderAsync` عند كلّ تغيير.
3. ✅ **زرّ إلغاء الحجز مشروط**: يظهر فقط لـ `pending`/`confirmed`.
4. ✅ **تقرير الفجوات** (هذا الملف).

المتبقّي: 15 صفحة دفعة ب + ج.
