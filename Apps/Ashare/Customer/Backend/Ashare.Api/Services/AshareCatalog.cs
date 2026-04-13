using ACommerce.OperationEngine.Core;

namespace Ashare.Api.Services;

/// <summary>
/// كتالوج أنواع العمليات الخاصة بعشير - محولة إلى كائنات OperationType type-safe.
/// الطلبات البرمجية تستخدم هذه الكائنات بدلاً من النصوص.
/// </summary>
public static class AshareOps
{
    // Listings
    public static readonly OperationType ListingCreate  = new("listing.create");
    public static readonly OperationType ListingPublish = new("listing.publish");
    public static readonly OperationType ListingDelete  = new("listing.delete");

    // Bookings
    public static readonly OperationType BookingCreate  = new("booking.create");
    public static readonly OperationType BookingCancel  = new("booking.cancel");

    // Payments
    public static readonly OperationType PaymentInitiate = new("payment.initiate");
    public static readonly OperationType PaymentCallback = new("payment.callback");
    public static readonly OperationType PaymentRefund   = new("payment.refund");

    // Subscriptions
    public static readonly OperationType SubscriptionCreate = new("subscription.create");
    public static readonly OperationType SubscriptionCancel = new("subscription.cancel");

    // Messages/Chat
    public static readonly OperationType ChatSend          = new("chat.send");
    public static readonly OperationType ConversationCreate = new("conversation.create");

    // Notifications
    public static readonly OperationType NotifySend = new("notify.send");
    public static readonly OperationType NotifyRead = new("notify.read");
}

/// <summary>
/// مفاتيح العلامات الخاصة بعشير.
/// </summary>
public static class AshareTags
{
    public static readonly TagKey ListingId     = new("listing_id");
    public static readonly TagKey CategoryId    = new("category_id");
    public static readonly TagKey BookingId     = new("booking_id");
    public static readonly TagKey PaymentId     = new("payment_id");
    public static readonly TagKey ConversationId = new("conversation_id");
    public static readonly TagKey MessageType   = new("message_type");
    public static readonly TagKey Currency      = new("currency");
    public static readonly TagKey Delivery      = new("delivery");
    public static readonly TagKey BillingCycle  = new("billing_cycle");
}

/// <summary>
/// كتالوج أنواع الحصص - تربط نوع العملية بنوع الحصة في IPlan.Quotas.
/// </summary>
public static class QuotaCheckKinds
{
    public static readonly TagValue ListingsCreate = new("listings.create");
    public static readonly TagValue ListingsFeature = new("listings.feature");
    public static readonly TagValue MessagesSend   = new("messages.send");
    public static readonly TagValue ApiCall        = new("api.call");
}

/// <summary>
/// أدوار الأطراف في قيود عشير.
/// </summary>
public static class AshareRoles
{
    public static readonly PartyRole Owner      = new("owner");
    public static readonly PartyRole Customer   = new("customer");
    public static readonly PartyRole Seller     = new("seller");
    public static readonly PartyRole Sender     = new("sender");
    public static readonly PartyRole Recipient  = new("recipient");
    public static readonly PartyRole Category   = new("category");
    public static readonly PartyRole Listing    = new("listing");
    public static readonly PartyRole Subscriber = new("subscriber");
    public static readonly PartyRole Plan       = new("plan");
}
