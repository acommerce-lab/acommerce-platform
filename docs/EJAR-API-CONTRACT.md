# Ejar — API Contract (Frontend-Driven)

This file is the **single source of truth for the HTTP routes the customer
frontend (`Ejar.Customer.UI`, hosted by Ejar.Web / Ejar.WebAssembly /
Ejar.Maui) expects from the Ejar customer backend (`Ejar.Api`)**.

Whenever a Razor page calls `Api.GetAsync(...)` / `Api.PostAsync(...)` or
the operation engine dispatches via `EjarRoutes`, the path on the right
must exist on the backend with the matching HTTP verb. This document
exists because the frontend is consumed by 3 hosts (Web, WASM, MAUI) and
we cannot change paths there easily — the backend adapts.

## Conventions

- `BaseUrl` is configured per host (e.g. `http://localhost:5300` in dev,
  `https://api.ejar.ye` in prod). All paths below are relative to it.
- Every response is an `OperationEnvelope<T>` (Law 2 from `docs/MODEL.md`).
- Mutations on protected resources require `Authorization: Bearer …` issued
  by `/auth/otp/verify`.
- Cross-cutting headers added by `Ejar.Web` for every request:
  - `X-App-Version`  (e.g. `1.0.0`) — read by `VersionGateInterceptor`.
  - `X-App-Platform` (`web` | `mobile` | `admin`) — same.
  - `Accept-Language` (`ar` / `en`) — read by translation interceptor.

## Endpoint matrix

Legend for **Owner**:
- `Kit:Auth`, `Kit:Chat`, `Kit:Notifications`, `Kit:Versions` — provided by a
  shared library; do not change route there.
- `App:Ejar.Api` — owned by the customer backend; route can be adjusted in
  Ejar.Api controllers.

### Auth (Kit:Auth)

| Method | Path                  | Body / Query                | Notes |
|--------|-----------------------|-----------------------------|-------|
| POST   | `/auth/otp/request`   | `{ phone }`                 | Initiates OTP. Returns `{ masked, expiresInSeconds }`. |
| POST   | `/auth/otp/verify`    | `{ phone, code }`           | Returns `{ token, userId, name, phone, role }`. |
| POST   | `/auth/logout`        | —                           | Bearer required. Returns `{ userId }`. |

### Health (App:Ejar.Api)

| Method | Path        | Notes |
|--------|-------------|-------|
| GET    | `/healthz`  | Liveness + DB reachability probe used by `api-diagnostics.js` on every page load. Returns `{ status, db, time, service, provider }`. |
| GET    | `/health`   | Backwards-compat alias for `/healthz` (older clients). |

### Versions (Kit:Versions)

| Method | Path                  | Notes |
|--------|-----------------------|-------|
| GET    | `/version/check`      | Reads `X-App-Version` / `X-App-Platform` headers. Returns `VersionCheckResult`. |

### Notifications (Kit:Notifications)

| Method | Path                                | Bearer | Notes |
|--------|-------------------------------------|--------|-------|
| GET    | `/notifications`                    | yes    | Returns `IReadOnlyList<NotificationItem>`. |
| POST   | `/notifications/{id}/read`          | yes    | Marks one as read. |
| POST   | `/notifications/read-all`           | yes    | Marks all as read. |

### Chat / Conversations (Kit:Chat)

| Method | Path                                       | Bearer | Notes |
|--------|--------------------------------------------|--------|-------|
| GET    | `/conversations`                           | yes    | Inbox. Returns `IReadOnlyList<IChatConversation>`. |
| GET    | `/conversations/{id}`                      | yes    | Conversation + messages. |
| POST   | `/conversations/{id}/messages`             | yes    | Sends a message. |
| POST   | `/chat/{convId}/enter`                     | yes    | Mutes notifications, marks user present. |
| POST   | `/chat/{convId}/leave`                     | yes    | Reverse of enter. |

### Listings — public discovery (App:Ejar.Api)

| Method | Path                              | Bearer | Notes |
|--------|-----------------------------------|--------|-------|
| GET    | `/home/view?city=`                | no     | Home aggregate (categories + featured + new). |
| GET    | `/home/explore?sort=&category=&q=`| no     | Card list for explore page. |
| GET    | `/home/search/suggestions`        | no     | Search hints (recent + popular). |
| GET    | `/listings`                       | no     | Filtered/paginated listings. Query: `city, district, propertyType, timeUnit, minPrice, maxPrice, q, lat, lng, radius, sort, page, pageSize`. |
| GET    | `/listings/{id:guid}`             | no     | Listing detail (with owner info). |
| GET    | `/cities`                         | no     | List of city names — bridge over discovery kit. |
| GET    | `/amenities`                      | no     | List of amenity slugs+labels — bridge over discovery kit. |
| GET    | `/categories`                     | no     | Category list — bridge over discovery kit. |

### Owner (current user) — listings (App:Ejar.Api)

| Method | Path                                  | Bearer | Notes |
|--------|---------------------------------------|--------|-------|
| GET    | `/my-listings`                        | yes    | Listings owned by the current user. |
| POST   | `/my-listings`                        | yes    | Create new listing. |
| POST   | `/my-listings/{id}/toggle`            | yes    | Pause/resume listing. |
| DELETE | `/my-listings/{id}`                   | yes    | Soft-delete. |

### Profile (App:Ejar.Api)

| Method | Path                  | Bearer | Notes |
|--------|-----------------------|--------|-------|
| GET    | `/me/profile`         | yes    | User profile + stats. |
| PUT    | `/me/profile`         | yes    | Update profile fields. |
| GET    | `/me/subscription`    | yes    | Active subscription summary. |
| GET    | `/me/invoices`        | yes    | List of invoices. |

### Favorites (App:Ejar.Api — bridges Kit:Favorites)

| Method | Path                              | Bearer | Notes |
|--------|-----------------------------------|--------|-------|
| GET    | `/favorites`                      | yes    | List user's favorited listings. |
| POST   | `/listings/{id}/favorite`         | yes    | Toggle favorite for a listing. |

### Bookings (App:Ejar.Api)

| Method | Path                  | Bearer | Notes |
|--------|-----------------------|--------|-------|
| GET    | `/bookings`           | yes    | User's bookings. |
| GET    | `/bookings/{id}`      | yes    | Booking details. |

### Plans / Subscriptions catalog (App:Ejar.Api)

| Method | Path                  | Notes |
|--------|-----------------------|-------|
| GET    | `/plans`              | List of subscription plans. |

### Complaints / Support (App:Ejar.Api — bridges Kit:Support)

| Method | Path                              | Bearer | Notes |
|--------|-----------------------------------|--------|-------|
| GET    | `/complaints`                     | yes    | User's support tickets. |
| GET    | `/complaints/{id}`                | yes    | Ticket + replies. |
| POST   | `/complaints`                     | yes    | File new ticket. |
| POST   | `/complaints/{id}/replies`        | yes    | Add a reply. |

### Legal (App:Ejar.Api)

| Method | Path             | Notes |
|--------|------------------|-------|
| GET    | `/legal`         | List of legal docs (key + label). |

## Operations registered in `EjarRoutes` (client dispatcher)

These are operation names → HTTP routes used when the client invokes
`ClientOpEngine.Dispatch(opName)`:

```
auth.otp.request        POST   /auth/otp/request
auth.otp.verify         POST   /auth/otp/verify
auth.logout             POST   /auth/logout
listing.toggle          POST   /my-listings/{listing_id}/toggle
listing.create          POST   /my-listings
listing.delete          DELETE /my-listings/{listing_id}
favorite.toggle         POST   /listings/{listing_id}/favorite
conversation.start      POST   /conversations/start
message.send            POST   /conversations/{conversation_id}/messages
notification.read       POST   /notifications/{notification_id}/read
notification.read.all   POST   /notifications/read-all
complaint.file          POST   /complaints
complaint.reply         POST   /complaints/{complaint_id}/replies
profile.update          PUT    /me/profile
```

## Backwards-compatibility rules

1. The frontend lives in three hosts (Web, WASM, MAUI). MAUI in particular
   ships compiled binaries to client devices — once shipped, paths are
   frozen for that version of the app.
2. The backend MUST keep accepting every path listed above as long as any
   shipped client version still calls it (see
   `docs/API-DRIFT-PREVENTION.md` for the strategy).
3. New endpoints can be added freely; existing ones must not be moved or
   renamed without coordinating with the frontend team and bumping the
   `X-App-Version` floor in `IVersionStore`.
