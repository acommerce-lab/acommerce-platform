using System.Globalization;
using System.Reflection;
using System.Resources;
using ACommerce.L10n.Blazor;
using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.Compositions.Customer.L10n.Resx;

/// <summary>
/// تَنفيذ <see cref="ITranslationProvider"/> يَقرأ مِن <c>ResourceManager</c>
/// (.resx + .{lang}.resx) في assembly مُعَيَّن.
///
/// <para><b>سُلوك "غير مَوجود"</b>: <see cref="TryTranslate"/> يُرجع <c>null</c>
/// لَو لَم يَجِد المِفتاح في أيّ مَلَفّ resx ⇒ <see cref="LayeredTranslationProvider"/>
/// يَنتَقِل لِلطَبَقَة التالِيَة. وَجَد المِفتاح في NEUTRAL لكن culture المَطلوبَة
/// تَنقُصها قيمَة ⇒ يَردّ NEUTRAL (ResourceManager fallback).</para>
/// </summary>
public sealed class ResxTranslationProvider : ITranslationProvider
{
    private readonly ResourceManager _rm;

    public ResxTranslationProvider(Assembly assembly, string baseName)
    {
        _rm = new ResourceManager(baseName, assembly);
    }

    public string? TryTranslate(string key, string language)
    {
        var culture = string.IsNullOrEmpty(language)
            ? CultureInfo.InvariantCulture
            : CultureInfo.GetCultureInfo(language);
        return _rm.GetString(key, culture);
    }
}

public static class ResxTranslationServiceCollectionExtensions
{
    /// <summary>
    /// يُضيف طَبَقَة تَرجَمَة مَدعومَة بِـ <c>.resx</c> إلى السِلسِلَة. آخِر
    /// <c>AddTranslationLayer</c> = أَعلى أَولَويّة في
    /// <see cref="LayeredTranslationProvider"/>. مَفاتيح غير مَوجودَة في
    /// طَبَقَة عُليا تَنزِلق تلقائيّاً لِأَدنى. التَطبيق يُسَجِّل:
    /// <code>
    /// services.AddTranslationLayer(typeof(MyTemplate).Assembly, "MyTemplate.Resources.Strings");
    /// services.AddTranslationLayer(typeof(MyApp).Assembly,      "MyApp.Resources.Strings");
    /// services.AddLayeredTranslation();   // مَرّة واحِدَة بَعد كلّ الـ layers
    /// </code>
    /// </summary>
    public static IServiceCollection AddTranslationLayer(
        this IServiceCollection services,
        Assembly assembly,
        string baseName)
    {
        // كلّ تَسجيل ⇒ instance جَديد. GetServices<ResxTranslationProvider>
        // يَجمَعها كلّها بِتَرتيب الإضافَة.
        services.AddScoped(_ => new ResxTranslationProvider(assembly, baseName));
        return services;
    }

    /// <summary>
    /// alias تَوافُق رَجعيّ لِـ F71. مُماثِل تَماماً لِـ
    /// <see cref="AddTranslationLayer"/>.
    /// </summary>
    [System.Obsolete("Use AddTranslationLayer for clarity. Behaviour is identical.")]
    public static IServiceCollection AddResxTranslationProvider(
        this IServiceCollection services,
        Assembly assembly,
        string baseName)
        => services.AddTranslationLayer(assembly, baseName);

    /// <summary>
    /// يُسَجِّل <see cref="LayeredTranslationProvider"/> كَـ
    /// <c>ITranslationProvider</c> النِهائيّ — يَلفّ كلّ
    /// <see cref="ResxTranslationProvider"/> المُسَجَّلَة عَبر
    /// <see cref="AddTranslationLayer"/>. يُستَدعى مَرّة واحِدَة بَعدَها.
    /// </summary>
    public static IServiceCollection AddLayeredTranslation(this IServiceCollection services)
    {
        services.AddScoped<ITranslationProvider>(sp =>
            new LayeredTranslationProvider(sp.GetServices<ResxTranslationProvider>()));
        return services;
    }
}
