using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Interceptors;

namespace ACommerce.Culture.Interceptors;

/// <summary>
/// OAM interceptor يَعمَل في طَور <see cref="InterceptorPhase.Post"/> عَلى
/// كلّ <c>http.send</c> ناجِح ويَفُكّ التَواريخ + العُملات + النُصوص في
/// المَغلَّف عَبر <see cref="CultureInterceptor"/> (الذي يَعتَمِد عَلى
/// ICultureContext). مَع هذا، أيّ طَلَب OAM (chat.message.send،
/// listings.search، …) يَحصُل عَلى تَوقيت المُستَخدِم تلقائيّاً —
/// الكيت أو الصَفحَة لا تَحتاج كتابَة سَطر واحِد.
///
/// <para>قَبل F62 كانَت <c>ApiReader</c> تَستَدعي
/// <c>CultureInterceptor.LocalizeAsync</c> يَدويّاً بَعد كلّ GET. مَع
/// انتِقال الـ HTTP إلى OAM، التَّعريب صار مُجَرَّد interceptor مُسَجَّل
/// في DI ⇒ التَطبيق لا يَلمسه، الكيتس لا تَلمسه.</para>
/// </summary>
public sealed class CultureLocalizationInterceptor : IOperationInterceptor
{
    private readonly CultureInterceptor _culture;
    public CultureLocalizationInterceptor(CultureInterceptor culture) => _culture = culture;

    public string Name => "culture-localization";
    public InterceptorPhase Phase => InterceptorPhase.Post;

    public bool AppliesTo(Operation op) => op.Type == "http.send";

    public Task<AnalyzerResult> InterceptAsync(
        OperationContext context,
        OperationResult? result = null)
    {
        // OperationEnvelope مَوضوع في ctx بَواسِطَة HttpDispatcher كَ "envelope".
        var envelope = context.Get<object>("envelope");
        if (envelope is null) return Task.FromResult(AnalyzerResult.Pass());

        // CultureInterceptor.LocalizeAsync<T> generic — نَستَدعيه عَبر reflection
        // بِما أنّ TX غير مَعروف هُنا (يَختَلِف لِكلّ kit endpoint).
        var envType = envelope.GetType();
        if (envType.Name != "OperationEnvelope`1") return Task.FromResult(AnalyzerResult.Pass());

        var generic = envType.GetGenericArguments()[0];
        var method  = typeof(CultureInterceptor).GetMethod(nameof(CultureInterceptor.LocalizeAsync))
                                                ?.MakeGenericMethod(generic);
        if (method is null) return Task.FromResult(AnalyzerResult.Pass());

        try { _ = method.Invoke(_culture, new[] { envelope, (object)false }); }
        catch { /* غير قاتِل */ }

        return Task.FromResult(AnalyzerResult.Pass());
    }
}
