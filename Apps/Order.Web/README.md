# Order — اوردر

Production-ready preview of the Order app (cafe & restaurant deals, in-store
or curbside pickup, no online payment) built **from scratch on top of**:

- **Server accounting libraries** — `ACommerce.OperationEngine` + the OpEngine
  `Entry.Create(...).From(...).To(...).Tag(...).Execute(...)` accounting
  pattern. Every meaningful state change in the API (place an order, send a
  message, send a notification) is an Operation that goes through the engine.
- **Client accounting libraries** — `ACommerce.OperationEngine.Wire`
  (`OperationEnvelope<T>`), `ACommerce.Client.Operations`,
  `ACommerce.Client.Http`, `ACommerce.Client.StateBridge`. The web client reads
  every backend response uniformly as an envelope.
- **Our widgets library** — `ACommerce.Widgets` + `ACommerce.Templates.Commerce`,
  including the Bootstrap-compat layer added in commit `9f0be68`. The whole
  Order shell is themed by overriding `--ac-*` once in `wwwroot/app.css` —
  no per-page styling.
- **Order's existing visual identity** — warm orange `#FF6B35` + amber
  `#F7931E` (lifted from `Apps/Order.Customer.App/Resources/Styles/Colors.xaml`).

## Layout

```
Apps/Order.Api/        Backend (ASP.NET Core 9 + SQLite + OpEngine + JWT + SMS 2FA)
  Entities/             User, Vendor, Category, Offer, OrderRecord, OrderItem,
                        Conversation, Message, Notification, Favorite,
                        TwoFactorChallengeRecord
  Controllers/          Auth, Categories, Vendors, Offers, Orders, Favorites,
                        Messages, Notifications, Users
  Services/             OrderSeeder (4 vendors, 13 offers, demo customer + chats),
                        JwtTokenStore, OrderPrincipal
  Program.cs            DI wiring (~170 lines, no Subscriptions/Payments/Files —
                        Order doesn't need them)
  appsettings.json      SQLite at data/order.db, JWT secret, port 5101
  data/                 SQLite database (created on first start)

Apps/Order.Web/        Blazor Web App (Server interactive)
  Components/
    App.razor           Loads widgets.css → templates.css → app.css cascade
    Routes.razor        Router with MainLayout default
    Layout/MainLayout   Phone-shaped shell + bottom nav (5 tabs) + auth restore
    Pages/              Home, OfferDetails, Cart, Checkout, MyOrders,
                        OrderDetails, Favorites, Messages, Conversation,
                        Notifications, Profile, Login, Settings  (13 pages)
  Services/             AuthStateService (with ProtectedLocalStorage persistence),
                        UiPreferences (theme + i18n), CartService, OrderApiClient
  wwwroot/
    app.css             Order brand override (#FF6B35) + shell + dark theme
    lib/bootstrap-icons Self-hosted (sandbox blocks the CDN)
    _framework/blazor.web.js  Self-hosted (the .NET 9 SDK doesn't ship it on
                              this image; same trick as Ashare.Web)
  Program.cs            HttpClient wiring + scoped services + interactive Blazor
  appsettings.json      OrderApi base = http://localhost:5101, port 5701
```

## What it covers

| Feature                           | Page                       | Backend route                            |
|-----------------------------------|----------------------------|------------------------------------------|
| Browse offers + categories        | `/`                        | `GET /api/offers` + `/api/categories`    |
| Offer details (favourite / chat)  | `/offer/{id}`              | `GET /api/offers/{id}`                   |
| Cart (single-vendor enforced)     | `/cart`                    | (client only)                            |
| Checkout — pickup type, car,      | `/checkout`                | `POST /api/orders` (OpEngine entry)      |
|   payment method, cash + change   |                            |                                          |
| Order success                     | `/order/{id}?just=1`       | `GET /api/orders/{id}`                   |
| My orders                         | `/orders`                  | `GET /api/orders/by-customer/{id}`       |
| Order details + cancel            | `/order/{id}`              | `GET /api/orders/{id}` + `/cancel`       |
| Favorites                         | `/favorites`               | `GET /api/favorites/by-user/{id}`        |
| Toggle favourite                  | (offer details)            | `POST /api/favorites/toggle`             |
| Messages list                     | `/messages`                | `GET /api/messages/conversations/by-user`|
| Conversation                      | `/chat/{id}`               | `GET/POST /api/messages/...`             |
| Start chat from offer             | (offer details)            | `POST /api/messages/conversations`       |
| Notifications + mark all read     | `/notifications`           | `GET/POST /api/notifications/...`        |
| Profile (avatar, stats, sign-out) | `/profile`                 |                                          |
| Settings (theme, language)        | `/settings`                | (local only — `UiPreferences`)           |
| Phone-OTP login                   | `/login`                   | `POST /api/auth/sms/request` + `/verify` |

The unfinished piece is exactly what the user said: **the SMS provider**.
Today the OTP is mocked — it's printed to the API logs as
`رمز التحقق: NNNNNN`. Plugging in a real Twilio/Unifonic provider is one
class swap inside `Order.Api/Program.cs` — the rest of the stack stays.

## Run it locally (no Docker)

```bash
bash Apps/Order.Web/run-local.sh
```

Opens:
- **Order Web** → http://localhost:5701
- **Order API** (Swagger) → http://localhost:5101/swagger

To sign in: phone `+966500000001` (Sara — seeded), the OTP is in
`/tmp/order-api.log`.

Stop with: `fuser -k 5101/tcp 5701/tcp`

## Run it with Docker (single command)

```bash
docker compose -f Apps/Order.Web/docker-compose.yml up --build
```

Both services come up; the API persists its SQLite file in
`Apps/Order.Web/_data/order.db`. Open http://localhost:5701.

## Smallest possible hosting (no Docker, no compose)

This is what we recommend pushing for the client preview today:

1. `dotnet publish Apps/Order.Api -c Release -o /opt/order/api`
2. `dotnet publish Apps/Order.Web -c Release -o /opt/order/web`
3. Two systemd units (sample below) — or `nohup … &` if it's truly throwaway.
4. Optional `nginx` in front mapping `https://demo.example.com → :5701`.

```ini
# /etc/systemd/system/order-api.service
[Service]
WorkingDirectory=/opt/order/api
ExecStart=/usr/bin/dotnet Order.Api.dll
Environment=ASPNETCORE_URLS=http://127.0.0.1:5101
Environment=ASPNETCORE_ENVIRONMENT=Production
Restart=always

[Install]
WantedBy=multi-user.target
```

```ini
# /etc/systemd/system/order-web.service
[Service]
WorkingDirectory=/opt/order/web
ExecStart=/usr/bin/dotnet Order.Web.dll
Environment=ASPNETCORE_URLS=http://127.0.0.1:5701
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=OrderApi__BaseUrl=http://127.0.0.1:5101
Restart=always

[Install]
WantedBy=multi-user.target
```

That's it — no payment provider, no message provider, no file storage, no
firebase. Plug those in only when the client signs off on the preview.
