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

## ما أُنجِز (دفعتان)

### الدفعة 1 (إصلاحات أوّلية):
1. ✅ **Nafath مُستَعاد** في Login: دائرة رقم عشوائيّ + عدّاد 120 ثانية + حالات.
2. ✅ **التبديل الحيّ للوضع الداكن والاتجاه** عبر JS interop.
3. ✅ **زرّ إلغاء الحجز مشروط** (pending/confirmed فقط).

### الدفعة 2 (Tier A + B + C دفعةً واحدة):

**Tier A — صفحات جديدة (4):**
1. ✅ **`/profile/edit` ProfileEdit** — avatar upload (InputFile + حدّ 2MB + معاينة)، تعديل الاسم/الهاتف/البريد/المدينة، تحقّق جوّال+بريد (محاكاة)، حفظ عبر PUT `/me/profile`.
2. ✅ **`/my-subscription` MySubscription** — خطّة نشطة + عدّاد أيّام + 4 بطاقات كوتا (listings/featured/images/api) مع progress bars، قائمة مزايا، قائمة فواتير، أزرار تجديد/تغيير/إلغاء، تحذير عند ≤7 أيّام.
3. ✅ **`/complaints` Complaints** — قائمة كاملة + نموذج شكوى جديدة inline (subject/body/priority/related entity)، فلاتر حالة.
4. ✅ **`/complaints/{id}` ComplaintDetail** — thread view: bubbles للمستخدم+الموظّف مع badge "موظّف دعم"، reply input (مُعطَّل للمحلولة/المغلقة)، POST `/complaints/{id}/replies`.

**Tier B — إعادة بناء inline بخطوات حقيقيّة (3):**
5. ✅ **`/book/{id}` NewBooking** — 4 خطوات: date (date picker + nights ±) → guests (guests counter + notes) → review (subtotal + service fee 5% + VAT 15% + terms checkbox) → payment (AcPaymentForm).
6. ✅ **`/create-listing` CreateListing** — 4 خطوات مع step indicator: category (grid tiles) → details (title/description/price/unit/capacity + amenity chips من `/amenities`) → images (InputFile multi + grid preview + main-image badge + reorder/delete + حدّ ImagesPerListing من الخطّة) → location (city/district + map sim).
7. ✅ **`/subscribe` Subscribe** — دورة فوترة (شهر/ربع ‑5%/سنة ‑20%) + promo code (ASHARE10/WELCOME20) + VAT 15% + ملخّص مفصّل + form.

**Tier C — تحسينات + ميزات مفقودة (11):**
8. ✅ **`/me` Me** — stats grid (bookings/listings/favorites/complaints) + 6 action cards + زرّ تعديل → ProfileEdit + رابط MySubscription.
9. ✅ **`/plans` Plans** — بطاقات خطط مُحسَّنة + ميزات بـ checks + جدول مقارنة (listings/featured/images/support).
10. ✅ **`/payment/callback` PaymentCallback** — بطاقة تأكيد معاملة (txnId + amount + date) + أزرار منفصلة للحجز vs الخطّة.
11. ✅ **`/legal` + `/legal/{key}` Legal** — accordion متعدّد المستندات (privacy/terms/refund) مع fetch من `/legal`.
12. ✅ **`/notifications` Notifications** — tabs فلاتر (الكل/حجوزات/رسائل/إعلانات/نظام) + تجميع بالتاريخ (اليوم/أمس/هذا الأسبوع/أقدم) + mark-read عند النقر + قراءة الكلّ.
13. ✅ **`/chats` Chats** — search box + unread badges + last-message preview + "منذ x" relative time.
14. ✅ **`/chat/{id}` ChatRoom** — attachment button (InputFile + preview chip) + typing indicator animation + محاكاة ردّ الشريك بعد 1.8s.
15. ✅ **`/my-listings` MyListings** — فلاتر حالة + per-card: views/bookings stats + أزرار (عرض/تعديل/إيقاف/ترقية لمميّز).
16. ✅ **`/bookings` Bookings** — فلاتر حالة (الكل/pending/confirmed/completed/cancelled).
17. ✅ **`/space/{id}` SpaceDetails** — gallery + favorite button + meta (location/capacity/rating) + amenities chips + map + owner card + sticky action bar (رجوع + احجز الآن).
18. ✅ **`/search` Search** — recent searches (مع حذف فردي) + popular + quick filters + grid فئات.

**Backend:**
- ✅ توسَّع `AshareV2Seed`: amenities + replies للشكاوى + subscription + invoices + profile + features للخطط.
- ✅ توسَّع `CatalogController`: `/amenities`, `/legal`, `/me/profile` (GET+PUT), `/me/subscription`, `/me/invoices`, `/complaints/{id}` + replies POST, `/complaints` create POST, unreadCount في conversations.

**i18n + CSS:**
- ✅ مفاتيح subscription/profile في `L.cs` بالعربيّة والإنجليزيّة.
- ✅ CSS جديد في `app.css`: step-indicator، progress-bar، category-pick، amenity-chips، images-grid، plans-compare table، chat composer/attach/typing، staff bubble، legal accordion، gallery placeholder، fav-btn، recent-chip.

**البناء**: ✅ API + Web يبنيان بلا أخطاء.
