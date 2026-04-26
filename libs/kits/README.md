# Kits — feature-folder layout, atomic libraries, distribution metas

Each Kit is a **feature folder** containing several focused csproj files
(Operations / State / Templates / Backend / Providers) plus a meta-package
that bundles them. Borrowed from Spree's `spree_*` gems and
`Microsoft.AspNetCore.App` meta-package.

## Folder layout

```
libs/kits/{Feature}/                                   ← one folder per feature
  ├── ACommerce.Kits.{Feature}.Backend/                ← drop-in Controllers + ports
  ├── ACommerce.Kits.{Feature}.Templates/              ← Razor components
  ├── ACommerce.Kits.{Feature}.State/                  ← Frontend state extensions (when needed)
  └── ACommerce.Kits.{Feature}/                        ← meta-package
```

Some features have **provider-specific** sub-kits with their own templates,
because the UI differs per provider (SMS phone+OTP ≠ Nafath QR-scan ≠ Email
magic-link). The shell template provides a slot; each provider kit ships
the slot fill:

```
libs/kits/{Feature}/                                   ← shell + abstraction
  └── ACommerce.Kits.{Feature}.Templates/              ← AcLoginPage with <Verifier> slot

libs/kits/{Feature}.{ProviderCategory}/                ← provider category
  └── {ProviderName}/                                  ← one folder per provider
      ├── ACommerce.Kits.{Feature}.{Cat}.{Prov}.Templates/   ← provider verifier UI
      ├── ACommerce.Kits.{Feature}.{Cat}.{Prov}/             ← provider meta
      └── ...
```

App combines a shell + chosen provider:
```razor
<AcLoginPage>
    <Verifier><SmsVerifierUi /></Verifier>     @* swap for <NafathVerifierUi/> etc. *@
</AcLoginPage>
```

## Kits in this repo today

```
libs/kits/
├── Chat/
│   ├── ACommerce.Kits.Chat.Backend/           ChatController + IChatStore
│   ├── ACommerce.Kits.Chat.Templates/         AcChatRoomPage Razor
│   └── ACommerce.Kits.Chat/                   meta (Operations + Client + Backend + Templates)
│
├── Auth/                                       ← shell — provider-agnostic
│   ├── ACommerce.Kits.Auth.Backend/           AuthController + IAuthUserStore + AuthKitJwtConfig
│   ├── ACommerce.Kits.Auth.Templates/         AcLoginPage with <Verifier> RenderFragment slot
│   └── ACommerce.Kits.Auth/                   meta (TwoFactor.Operations + Backend + Templates)
│
├── Auth.TwoFactor/                             ← 2FA provider category
│   └── Sms/                                    ← one folder per provider
│       ├── ACommerce.Kits.Auth.TwoFactor.Sms.Templates/   SmsVerifierUi Razor
│       └── ACommerce.Kits.Auth.TwoFactor.Sms/            meta (Sms.Mock + Templates)
│
└── Notifications/
    ├── ACommerce.Kits.Notifications.Backend/  NotificationsController + INotificationStore
    └── ACommerce.Kits.Notifications/          meta (Operations + InApp + Firebase + Backend)
```

Future provider folders (planned but not built):
- `Auth.TwoFactor/Nafath/` — Nafath provider + `<NafathVerifierUi />` (QR + status polling).
- `Auth.TwoFactor/Email/` — Email magic-link provider + `<EmailVerifierUi />`.
- `Files/Local/` and `Files/AliyunOss/` and `Files/GoogleCloud/` — file-storage providers.
- `Payments/Noon/` and `Payments/Stripe/` — payment providers, each with checkout templates.

## Why feature folders + provider folders

Three reasons the user's instinct is right:

1. **Discoverability** — finding "all SMS-related code" = browse one folder.
2. **Provider isolation** — the SMS package doesn't drag Nafath into the
   build graph. Apps that don't need Nafath don't reference it; the SMS
   atom is small.
3. **Common-shell + provider-slot pattern** — the login screen layout is
   identical across providers (header + body + footer), but the actual
   verification step differs entirely. Shell-with-slot keeps DRY without
   forcing one shape on every provider.

## Rule for provider kits

A provider kit is justified when **it has its own UI** that the
shell-template can't anticipate. SMS (phone + numeric pad), Nafath
(scan-and-wait), Email (magic-link), Apple-Pay (tokenised flow) — all yes.

A provider kit is NOT justified when it's an interchangeable backend impl
with the same surface (SMS-via-Twilio vs SMS-via-Unifonic — same UI, just
a different concrete `ITwoFactorChannel`). Those stay as
`*.Providers.*` libs at the backend layer.

## Versioning rule

All Kit packages share one version, evolve together (monoversion). No
package can pin to a different minor than its meta — avoids the dependency
hell that hit Spree consumers.
