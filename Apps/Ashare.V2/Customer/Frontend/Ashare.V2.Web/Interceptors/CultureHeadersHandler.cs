using System.Net.Http.Headers;
using Ashare.V2.Web.Store;

namespace Ashare.V2.Web.Interceptors;

/// <summary>
/// معترض الثقافة (جانب الذهاب): يُضاف إلى سلسلة معالجات HttpClient، فيُختم
/// كلّ طلب صادر برؤوس ثقافة المستخدم لتَفهَم الخدمة الخلفيّة السياق:
///   <c>Accept-Language: ar</c>
///   <c>X-User-Timezone: Asia/Riyadh</c>
///   <c>X-User-Currency: SAR</c>
///
/// الخدمة الخلفيّة تستعملها لتصحيح التواريخ الواردة، ترجمة المحتوى المخزَّن،
/// تحويل الأرقام حسب العملة، إلخ. يُنظر في <c>CurrentCultureMiddleware</c>.
/// </summary>
public sealed class CultureHeadersHandler : DelegatingHandler
{
    private readonly AppStore _store;
    public CultureHeadersHandler(AppStore store) => _store = store;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var c = _store.Ui.Culture;
        request.Headers.AcceptLanguage.Clear();
        request.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue(c.Language));

        request.Headers.Remove("X-User-Timezone");
        request.Headers.Add("X-User-Timezone", c.TimeZone);

        request.Headers.Remove("X-User-Currency");
        request.Headers.Add("X-User-Currency", c.Currency);

        return base.SendAsync(request, cancellationToken);
    }
}
