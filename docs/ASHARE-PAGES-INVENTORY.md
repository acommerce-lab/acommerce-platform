# حصر شامل لصفحات عشير القديم (V1) ← Ashare.V2

الحصر مبنيّ على مسح كامل لـ
`acommerce-lab/ACommerce.Libraries/Apps/Ashare.Shared/Components/`
و`Apps/Ashare.App/Components/` (MAUI shell).

## جدول الصفحات الكامل

| # | مسار V1 | مسار V2 | حالة | ملاحظات |
|---|---|---|---|---|
| 1 | `Pages/Home.razor` | `/` | ✅ | hero + categories + featured + new + CTA + actions |
| 2 | `Pages/Explore.razor` | `/explore` | ✅ | filter/sort/view (list+grid+map) |
| 3 | `Pages/Search.razor` | `/search` | ✅ | recent + popular + quick filters |
| 4 | `Pages/SpaceDetails.razor` | `/space/{id}` | ✅ | gallery + owner + amenities + sticky bar |
| 5 | `Pages/Notifications.razor` | `/notifications` | ✅ | list + mark-all-read |
| 6 | `Pages/Favorites.razor` | `/favorites` | ⏳ | list of favorited listings |
| 7 | `Pages/Bookings.razor` | `/bookings` | ⏳ | my bookings list |
| 8 | `Pages/BookingCreate.razor` | `/book/{id}` | ⏳ | wizard: dates + capacity + summary |
| 9 | `Pages/BookingDetails.razor` | `/booking/{id}` | ⏳ | booking + status + actions |
| 10 | `Pages/Auth/Login.razor` (Nafath) | `/login` | ⏳ | Nafath + SMS fallback |
| 11 | `Pages/Auth/Register.razor` | `/register` | ⏳ | phone + name + city |
| 12 | `Pages/Auth/ProfileEdit.razor` | `/me/edit` | ⏳ | edit personal info |
| 13 | `Pages/Profile.razor` | `/me` | ⏳ | profile card + links |
| 14 | `Pages/Chat/Chats.razor` | `/chats` | ⏳ | conversations list |
| 15 | `Pages/Chat/ChatRoom.razor` | `/chat/{id}` | ⏳ | bubble conversation |
| 16 | `Pages/Complaints.razor` | `/help` | ⏳ | complaints list |
| 17 | `Pages/ComplaintDetails.razor` | `/help/{id}` | ⏳ | thread of replies |
| 18 | `Pages/CreateListing.razor` | `/create-listing` | ⏳ | form + photos + attributes |
| 19 | `Pages/Host/MySpaces.razor` | `/my-listings` | ⏳ | owner's listings |
| 20 | `Pages/Host/SubscriptionPlans.razor` | `/plans` | ⏳ | available plans |
| 21 | `Pages/Host/SubscriptionCheckout.razor` | `/subscribe/{planId}` | ⏳ | payment form |
| 22 | `Pages/Host/SubscriptionDashboard.razor` | `/host/dashboard` | ⏳ | owner dashboard |
| 23 | `Pages/Host/PaymentCallback.razor` | `/payment/callback` | ⏳ | return URL from gateway |
| 24 | `Pages/Language.razor` | `/settings/language` | ⏳ | language switch (ar/en) |
| 25 | `Pages/LegalPageView.razor` | `/legal/{key}` | ⏳ | static legal content |
| 26 | `Pages/Settings.razor` (existing) | `/settings` | ⏳ | theme + language + sign-out |
| 27 | `Pages/Host/PaymentPage.xaml` (MAUI) | `/payment/process` | ⏳ | payment simulation flow |
| 28 | `AppStartup.razor` (version gate) | app-level guard `/version-gate` | ⏳ | spinner / update-required / normal |

## الخدمات + الودجات المُساندة

| # | V1 | V2 | حالة |
|---|---|---|---|
| S1 | `IAppVersionService` + `VersionCheckService` | backend `/version/check` + `AcVersionGate` | ⏳ |
| S2 | `ThemeService` | `AppStore.Ui.Theme` + `AcThemeToggle` widget | ⏳ |
| S3 | `LocalizationService` | `AppStore.Ui.Language` + `L` helper | ⏳ |
| S4 | `TokenStorageService` | `AppStore.Auth.AccessToken` (في الذاكرة) | ⏳ |
| S5 | `ITrackingConsentService` | `AppStore.Ui.TrackingConsent` | ⏳ (مُؤجَّل) |
| S6 | `PendingListingService` | `AppStore.DraftListing` | ⏳ |
| S7 | `IAppNavigationService` | `NavigationManager` مباشرة | ✅ |
| W1 | City-picker في Hero | `AcCityPicker` widget | ⏳ |
| W2 | Map Leaflet | `AcMapSim` (placeholder للاستبدال) | ✅ |
| W3 | Bottom-sheet | `AcBottomSheet` | ✅ |
| W4 | Modal | `AcModal` | ✅ |
| W5 | Cultural patterns (5) | `.acm-pattern-*` SVG | ⬜ (تزيينيّ، لاحقاً) |

## العمليات (Entries) اللازم إصدارها

| Entry | From | To | Tags | الدور |
|---|---|---|---|---|
| `auth.nafath.request` | User | IdentityProvider:nafath | national_id | requires `INafathChannel` |
| `auth.nafath.verify` | User | IdentityProvider:nafath | otp | — |
| `auth.sms.request` | User | IdentityProvider:sms | phone | — |
| `auth.sms.verify` | User | IdentityProvider:sms | code | — |
| `auth.sign_out` | User | App | — | local |
| `user.profile.update` | User | User:{id} | fields | — |
| `listing.view` | User | Listing:{id} | — | local (no route) |
| `listing.favorite` | User | Listing:{id} | state | POST /listings/{id}/favorite |
| `listing.share` | User | Listing:{id} | channel | local |
| `listing.create` | User | Listing:{id} | category, price, district | POST /listings |
| `listing.delete` | User | Listing:{id} | — | DELETE /listings/{id} |
| `booking.start` | User | Listing:{id} | — | local (navigation) |
| `booking.create` | User | Listing:{id} | nights, capacity | POST /bookings (parent) |
| `booking.cancel` | User | Booking:{id} | reason | POST /bookings/{id}/cancel |
| `payment.charge` (sub) | User | Vendor:{id} | amount, currency, method | POST /payments requires `IPaymentGateway` |
| `payment.callback` | PaymentGateway | Booking:{id} | status | POST /payments/callback |
| `subscription.subscribe` | User | Plan:{id} | — | POST /subscriptions |
| `subscription.cancel` | User | Subscription:{id} | reason | POST /subscriptions/{id}/cancel |
| `category.select` | User | Category:{id} | — | local |
| `catalog.search` | User | Catalog | q | local |
| `catalog.filter` | User | Catalog | category, price, capacity, rating | local |
| `complaint.file` | User | Vendor:{id} | subject, severity | POST /complaints |
| `complaint.reply` | Author | Complaint:{id} | — | POST /complaints/{id}/replies |
| `message.send` | User | Conversation:{id} | text | POST /conversations/{id}/messages |
| `conversation.mark_read` | User | Conversation:{id} | — | POST /conversations/{id}/read |
| `notification.mark_read` | User | Notification:{id} | — | POST /notifications/{id}/read |
| `ui.set_language` | User | App | lang | local (ApplyLocalAsync) |
| `ui.set_theme` | User | App | theme | local (ApplyLocalAsync) |
| `ui.set_city` | User | App | city | local (ApplyLocalAsync) |
| `app.version.check` | App | VersionChannel | current, build | requires `IAppVersionChannel` |
| `app.version.block` | App | User | latest, store_url | local |
| `tracking.consent.grant` | User | ConsentStore | — | local |
| `tracking.consent.revoke` | User | ConsentStore | — | local |

## البذر الموسَّع (V1 + V2)

| نوع | V1 (DB) | V2 (in-memory) |
|---|---|---|
| Users | 3 (OwnerAhmed + CustomerSara + Admin) | — (V2 demo) |
| Categories | 5 (Residential, LookingFor…, Administrative, Commercial) | 5 (apartment, room, studio, villa, shared) |
| Listings | 24 (16 real + 8 test) | 10 مقتبسة |
| Bookings | — (تُبذَر الآن) | 3 (active, pending, completed) |
| Conversations + Messages | — (تُبذَر الآن) | 2 conversations × ~3 messages |
| Complaints | — | 2 عيّنات |
| Notifications | 7 × 2 users (مُضافة حديثاً) | 7 × 1 user |
| Plans | مُبذَّرة (3 خطط) | 3 خطط (Basic, Pro, Enterprise) |
| Subscriptions | — | — |
| Cities | — | 8 (الرياض، جدة، مكة، المدينة، الدمام، الخبر، القصيم، أبها) |
| PopularSearches | — | 7 (مُضافة) |
| LegalDocs | — | 3 (privacy, terms, return) |
