namespace ACommerce.Bookings.Operations;

/// <summary>Well-known booking status codes.</summary>
public static class BookingStatuses
{
    public const int Pending = 0;
    public const int Confirmed = 1;
    public const int AwaitingPayment = 2;
    public const int Paid = 3;
    public const int Cancelled = 4;
    public const int Completed = 5;
    public const int Refunded = 6;

    public static string Label(int s) => s switch
    {
        Pending => "Pending",
        Confirmed => "Confirmed",
        AwaitingPayment => "AwaitingPayment",
        Paid => "Paid",
        Cancelled => "Cancelled",
        Completed => "Completed",
        Refunded => "Refunded",
        _ => $"Unknown({s})"
    };
}
