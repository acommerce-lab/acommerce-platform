using AshareMigrator.Legacy;
using AshareMigrator.Target;

namespace AshareMigrator.Mappers;

public static class BookingMapper
{
    /// <summary>
    /// يحوّل LegacyBooking إلى NewBooking.
    /// SpaceId → ListingId، HostId → OwnerId، CustomerId من نص إلى Guid عبر قاموس بحث.
    /// CheckInDate/CheckOutDate → StartDate/EndDate، CustomerNotes → Notes.
    /// حقل DepositAmount + GuestsCount + SpaceName + HostNotes تُحفظ في حقل Notes كنص إضافي لأنها غير موجودة في الكيان الجديد.
    /// </summary>
    public static NewBooking Map(
        LegacyBooking src,
        Guid customerId)
    {
        var notes = BuildNotes(src);

        return new NewBooking
        {
            Id = src.Id,
            CreatedAt = DateTime.SpecifyKind(src.CreatedAt, DateTimeKind.Utc),
            UpdatedAt = src.UpdatedAt,
            IsDeleted = false,
            ListingId = src.SpaceId,
            CustomerId = customerId,
            OwnerId = src.HostId,
            StartDate = DateTime.SpecifyKind(src.CheckInDate, DateTimeKind.Utc),
            EndDate = DateTime.SpecifyKind(src.CheckOutDate, DateTimeKind.Utc),
            TotalPrice = src.TotalPrice,
            Currency = string.IsNullOrWhiteSpace(src.Currency) ? "SAR" : src.Currency,
            Status = MapStatus(src.Status),
            Notes = notes,
        };
    }

    /// <summary>
    /// BookingStatus القديم: Pending=0, Confirmed=1, CheckedIn=2, Completed=3, Cancelled=4, NoShow=5
    /// BookingStatus الجديد: Pending=0, Confirmed=1, InProgress=2, Completed=3, Cancelled=4, Disputed=5
    /// </summary>
    private static int MapStatus(int legacy) => legacy switch
    {
        0 => 0, 1 => 1, 2 => 2, 3 => 3, 4 => 4, 5 => 4, _ => 0
    };

    private static string? BuildNotes(LegacyBooking src)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(src.CustomerNotes)) parts.Add($"عميل: {src.CustomerNotes}");
        if (!string.IsNullOrWhiteSpace(src.HostNotes)) parts.Add($"مضيف: {src.HostNotes}");
        if (src.GuestsCount > 0) parts.Add($"عدد الضيوف: {src.GuestsCount}");
        if (src.DepositAmount > 0) parts.Add($"العربون: {src.DepositAmount} {src.Currency}");
        if (!string.IsNullOrWhiteSpace(src.SpaceName)) parts.Add($"اسم المكان: {src.SpaceName}");
        return parts.Count == 0 ? null : string.Join(" | ", parts);
    }
}
