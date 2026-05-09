namespace ACommerce.L10n.Blazor;

/// <summary>
/// Provider يَلفّ سِلسِلَة <see cref="ITranslationProvider"/> ويَختار
/// أَوَّل طَبَقَة تُجيب. ترتيب الطَبَقات بِالأَولَويّة العَكسيّة لِلتَسجيل:
/// آخِر <c>AddTranslationLayer</c> = أَعلى أَولَويّة.
///
/// <para><b>الـ override pattern</b>: التَطبيق يُسَجِّل layer-ه بَعد layer
/// القالَب ⇒ مَفاتيح مَوجودَة في app's resx تُغطّي مَفاتيح القالَب،
/// مَفاتيح مَفقودَة (لَم يُغَطِّها التَطبيق) تَنزِلق إلى القالَب تلقائيّاً.
/// لا حاجَة لِلتَطبيق أَن يُكَرِّر كلّ مِفتاح — يُكَرِّر فَقَط ما يُريد تَخصيصه.</para>
///
/// <example>
/// <code>
/// // التَطبيق:
/// services.AddTranslationLayer(templateAsm, "MyTemplate.Resources.Strings"); // floor
/// services.AddTranslationLayer(appAsm,      "MyApp.Resources.Strings");      // override
/// // L["app.name"] ⇒ يَفحَص MyApp أَوَّلاً، ثُمّ MyTemplate.
/// </code>
/// </example>
/// </summary>
public sealed class LayeredTranslationProvider : ITranslationProvider
{
    private readonly IReadOnlyList<ITranslationProvider> _orderedHighToLow;

    public LayeredTranslationProvider(IEnumerable<ITranslationProvider> layers)
    {
        // Reverse: آخِر مُسَجَّل في DI = أَعلى أَولَويّة.
        // تَطبيق يُسَجِّل layer-ه بَعد القالَب ⇒ يَفوز.
        _orderedHighToLow = layers.Reverse().ToList();
    }

    public string? TryTranslate(string key, string language)
    {
        foreach (var layer in _orderedHighToLow)
        {
            var v = layer.TryTranslate(key, language);
            if (v is not null) return v;
        }
        return null;
    }
}
