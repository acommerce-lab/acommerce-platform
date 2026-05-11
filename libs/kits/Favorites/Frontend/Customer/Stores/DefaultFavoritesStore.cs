using System.Text.Json;
using ACommerce.Client.Operations;

namespace ACommerce.Kits.Favorites.Frontend.Customer.Stores;

/// <summary>
/// OAM-shaped (F61). favorite.toggle مَوسوم بـ realtime_broadcast فتَحقن
/// composition Realtime مُعتَرضاً يُعلِم الأَجهزة الأُخرى لِنَفس المُستَخدِم.
/// </summary>
public sealed class DefaultFavoritesStore : IFavoritesStore
{
    private readonly ITemplateEngine _engine;
    private HashSet<string> _ids = new();

    public DefaultFavoritesStore(ITemplateEngine engine) => _engine = engine;

    public IReadOnlyCollection<string> Ids => _ids;
    public bool IsLoading { get; private set; }
    public event Action? Changed;

    public async Task LoadAsync(CancellationToken ct = default)
    {
        IsLoading = true; Changed?.Invoke();
        try
        {
            // الباك يَردّ rows غَنيّة (Id + Title + Price + …) لا List<string>
            // عاريَة. نَقبَل الشَكلَين عَبر JsonElement: سَلسِلَة (List<string>)
            // أو كائنات لَها حَقل "Id" / "id". الاكتِفاء بِـ List<string> كان
            // يَكسِر LoadAsync مَع InvalidCast→StartObject، فَتَبقى Ids فارِغَة
            // وَلا يَتَلَوَّن قَلب أَيّ بِطاقَة بِالأَحمَر إلّا في صَفحَة
            // /favorites التي تُنفِّذ deserialization مُستَقِلّاً.
            var env = await _engine.ExecuteAsync<List<JsonElement>>(FavoritesOps.List(), ct: ct);
            if (env.Operation.Status == "Success" && env.Data is not null)
                _ids = env.Data.Select(ExtractId).Where(x => x is not null).Cast<string>().ToHashSet();
        }
        finally { IsLoading = false; Changed?.Invoke(); }
    }

    private static string? ExtractId(JsonElement el) => el.ValueKind switch
    {
        JsonValueKind.String => el.GetString(),
        JsonValueKind.Object =>
            el.TryGetProperty("Id", out var pid) ? pid.GetString()
            : el.TryGetProperty("id", out var lid) ? lid.GetString()
            : null,
        _ => null,
    };

    public async Task ToggleAsync(string targetId, CancellationToken ct = default)
    {
        var env = await _engine.ExecuteAsync<ToggleResultDto>(FavoritesOps.Toggle(targetId), ct: ct);
        if (env.Operation.Status != "Success" || env.Data is null) return;
        // backend يُرجِع IsFavorite (مُفرَد) — لا IsFavorited. عَدَم
        // التَطابُق كان يَجعَل القَلب لا يَتَلَوَّن أَبَداً.
        if (env.Data.IsFavorite) _ids.Add(targetId);
        else                      _ids.Remove(targetId);
        Changed?.Invoke();
    }

    public bool IsFavorited(string targetId) => _ids.Contains(targetId);

    /// <summary>مَدخَل realtime: composition تَدفَع تَغيُّراً مِن جِهاز آخَر.</summary>
    public void IngestRealtimeToggle(string listingId, bool isFavorited)
    {
        if (isFavorited) _ids.Add(listingId);
        else              _ids.Remove(listingId);
        Changed?.Invoke();
    }

    /// <summary>
    /// مُطابِق لِـ <c>FavoriteToggleResult(string Id, bool IsFavorite)</c> في
    /// backend Favorites kit. لا تُغَيِّر الأَسماء — JSON مَطابِقَة دَقيقَة
    /// بِغَضّ النَّظَر عَن case-insensitive في الـ deserializer.
    /// </summary>
    private sealed record ToggleResultDto(string? Id, bool IsFavorite);
}
