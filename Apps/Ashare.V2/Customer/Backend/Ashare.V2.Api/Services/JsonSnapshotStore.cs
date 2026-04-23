using System.Text.Json;

namespace Ashare.V2.Api.Services;

/// <summary>
/// حفظ واستعادة الحالة القابلة للتغيير إلى ملف JSON محليّ.
/// يُعوّض غياب قاعدة البيانات في البيئة التجريبيّة.
/// </summary>
internal static class JsonSnapshotStore
{
    private const string FilePath = "data/ashare-v2.json";
    private static readonly SemaphoreSlim _sem = new(1, 1);
    private static readonly JsonSerializerOptions _opts = new()
    {
        WriteIndented = false,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>تسلسل الحالة الحاليّة وكتابتها إلى القرص (fire-and-forget-safe).</summary>
    public static async Task SaveAsync()
    {
        await _sem.WaitAsync();
        try
        {
            Directory.CreateDirectory("data");
            var snap = new SnapshotDto
            {
                Listings      = AshareV2Seed.Listings.ToList(),
                Bookings      = AshareV2Seed.Bookings.ToList(),
                Reviews       = AshareV2Seed.Reviews.ToList(),
                FavoriteIds   = AshareV2Seed.FavoriteIds.ToList(),
                Notifications = AshareV2Seed.Notifications.ToList(),
                ProfileName   = AshareV2Seed.Profile.FullName,
                ProfileEmail  = AshareV2Seed.Profile.Email,
                ProfilePhone  = AshareV2Seed.Profile.Phone,
                ProfileCity   = AshareV2Seed.Profile.City
            };
            var json = JsonSerializer.Serialize(snap, _opts);
            await File.WriteAllTextAsync(FilePath, json);
        }
        catch { /* snapshot failures are non-critical */ }
        finally { _sem.Release(); }
    }

    /// <summary>استعادة الحالة من القرص عند بدء التشغيل (إن وُجد ملف).</summary>
    public static async Task RestoreAsync()
    {
        if (!File.Exists(FilePath)) return;
        try
        {
            var json = await File.ReadAllTextAsync(FilePath);
            var snap = JsonSerializer.Deserialize<SnapshotDto>(json, _opts);
            if (snap is null) return;

            if (snap.Listings?.Count > 0)
            { AshareV2Seed.Listings.Clear(); AshareV2Seed.Listings.AddRange(snap.Listings); }

            if (snap.Bookings?.Count > 0)
            { AshareV2Seed.Bookings.Clear(); AshareV2Seed.Bookings.AddRange(snap.Bookings); }

            if (snap.Reviews?.Count > 0)
            { AshareV2Seed.Reviews.Clear(); AshareV2Seed.Reviews.AddRange(snap.Reviews); }

            if (snap.FavoriteIds?.Count > 0)
            { AshareV2Seed.FavoriteIds.Clear(); foreach (var id in snap.FavoriteIds) AshareV2Seed.FavoriteIds.Add(id); }

            if (snap.Notifications?.Count > 0)
            { AshareV2Seed.Notifications.Clear(); AshareV2Seed.Notifications.AddRange(snap.Notifications); }

            if (snap.ProfileName is not null)
                AshareV2Seed.Profile = AshareV2Seed.Profile with
                {
                    FullName = snap.ProfileName,
                    Email    = snap.ProfileEmail ?? AshareV2Seed.Profile.Email,
                    Phone    = snap.ProfilePhone ?? AshareV2Seed.Profile.Phone,
                    City     = snap.ProfileCity  ?? AshareV2Seed.Profile.City
                };
        }
        catch { /* بيانات تالفة — نتجاهل ونبدأ بالبذور */ }
    }

    private sealed class SnapshotDto
    {
        public List<AshareV2Seed.ListingSeed>?       Listings      { get; set; }
        public List<AshareV2Seed.BookingSeed>?       Bookings      { get; set; }
        public List<AshareV2Seed.ReviewSeed>?        Reviews       { get; set; }
        public List<string>?                         FavoriteIds   { get; set; }
        public List<AshareV2Seed.NotificationSeed>?  Notifications { get; set; }
        public string? ProfileName  { get; set; }
        public string? ProfileEmail { get; set; }
        public string? ProfilePhone { get; set; }
        public string? ProfileCity  { get; set; }
    }
}
