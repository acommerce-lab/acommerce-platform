namespace ACommerce.Compositions.Customer.Timezone.Js;

/// <summary>
/// مُزَوِّد المِنطَقَة الزَمَنيّة لِلواجِهَة. يُحَوِّل DateTime UTC إلى
/// مَحَلّيّ + يُنَسِّق "manaaِق نِسبيّ" (الآن، 5د، 2س، أَمس، …).
///
/// <para>التَنفيذ الافتراضيّ <see cref="JsTimezoneProvider"/> يَستَخدِم JS
/// interop (acTz.offset/name مِن الـ wwwroot module) لِجَلب offset المُتَصَفِّح.</para>
/// </summary>
public interface ITimezoneProvider
{
    Task InitAsync();
    DateTime ToLocal(DateTime utc);
    string FormatTime(DateTime utc);
    string FormatRelative(DateTime utc);
}
