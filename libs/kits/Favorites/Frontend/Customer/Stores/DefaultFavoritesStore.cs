using System.Text.Json;
using System.Text.Json.Serialization;
using ACommerce.Client.Operations;
using ACommerce.OperationEngine.Wire;

namespace ACommerce.Kits.Favorites.Frontend.Customer.Stores;

/// <summary>
/// OAM-shaped (F61). favorite.toggle مَوسوم بـ realtime_broadcast فتَحقن
/// composition Realtime مُعتَرضاً يُعلِم الأَجهزة الأُخرى لِنَفس المُستَخدِم.
///
/// <para><b>Toggle Strategy</b> — Optimistic Flip:</para>
/// <list type="number">
///   <item>اقلِب <c>_ids</c> فَوراً (UI أَحمَر/فاضي يَتَجاوَب لَحظِيّاً).</item>
///   <item>أَطلِق Changed لِيُعيد كُلّ Card رَسم الحالَة.</item>
///   <item>اِرسِل الـ op لِلخادِم.</item>
///   <item>إن خالَف الـ server صَراحَةً، صَحِّح. إن صَمَت أَو وافَق، اِبقَ.</item>
/// </list>
/// <para>هذا يَحمي ضِدّ مُشكِلَتَين شائِعَتَين: (أ) deserialization يَفشَل
/// في حَقل bool فَيُرجِع false دائِماً ⇒ كان كُلّ click يُحَوِّل إلى "غَير مُفَضَّل"
/// مَهما كانَت الحالَة الفِعليَّة؛ (ب) رِحلَة الشَبَكَة الطَويلَة كانَت تَجعَل
/// المُستَخدِم يَنقُر مَرَّتَين ⇒ ذهاب-إياب فِعلي. مَكان عامّ: كُلّ apps
/// تَستَخدِم <c>IFavoritesStore</c> تَستَفيد بِلا تَغيير.</para>
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
            // عاريَة. نَقبَل الشَكلَين عَبر JsonElement.
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
        // ① Optimistic flip — اقلِب فَوراً قَبل HTTP. الـ UI يَتَجاوَب لَحظِيّاً.
        var wasFav = _ids.Contains(targetId);
        var optimisticState = !wasFav;
        if (optimisticState) _ids.Add(targetId);
        else                  _ids.Remove(targetId);
        Changed?.Invoke();

        // ② اِرسِل لِلخادِم.
        OperationEnvelope<ToggleResultDto>? env = null;
        try
        {
            env = await _engine.ExecuteAsync<ToggleResultDto>(FavoritesOps.Toggle(targetId), ct: ct);
        }
        catch
        {
            // فَشَل HTTP ⇒ revert الـ optimistic flip.
            if (wasFav) _ids.Add(targetId);
            else         _ids.Remove(targetId);
            Changed?.Invoke();
            throw;
        }

        // ③ Reconcile مَع الخادِم — فَقَط لَو خالَف صَراحَةً.
        if (env.Operation.Status != "Success")
        {
            // فَشَل OAM ⇒ revert.
            if (wasFav) _ids.Add(targetId);
            else         _ids.Remove(targetId);
            Changed?.Invoke();
            return;
        }

        if (env.Data is { } d && d.IsFavorite != optimisticState)
        {
            // خادِم يَقول العَكس ⇒ صَحِّح إلى حالَة الخادِم.
            if (d.IsFavorite) _ids.Add(targetId);
            else               _ids.Remove(targetId);
            Changed?.Invoke();
        }
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
    /// backend. JsonPropertyName صَريح لِيَحمي ضِدّ أَيّ تَغيير لاحِق في
    /// JsonSerializerOptions الافتِراضِيَّة (camelCase / case-sensitivity).
    /// </summary>
    private sealed record ToggleResultDto(
        [property: JsonPropertyName("id")]         string? Id,
        [property: JsonPropertyName("isFavorite")] bool    IsFavorite);
}
