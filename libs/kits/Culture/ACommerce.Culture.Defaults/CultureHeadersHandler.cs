using System.Net.Http.Headers;
using ACommerce.Culture.Abstractions;

namespace ACommerce.Culture.Defaults;

/// <summary>
/// DelegatingHandler يُلصِق هَيدرات الثَقافة عَلى كلّ طَلَب صادِر:
/// <list type="bullet">
///   <item><c>Accept-Language</c> ← <see cref="ICultureContext.Language"/></item>
///   <item><c>X-User-Timezone</c> ← <see cref="ICultureContext.TimeZoneId"/></item>
///   <item><c>X-User-Currency</c> ← <see cref="ICultureContext.Currency"/></item>
///   <item><c>X-Numeral-System</c> ← <see cref="ICultureContext.NumeralSystem"/></item>
/// </list>
/// التَطبيق يُسَجِّل تَنفيذ <see cref="ICultureContext"/> ⇒ كلّ طَلَب يَخرُج
/// بِالقِيَم الحاليّة.
/// </summary>
public sealed class CultureHeadersHandler : DelegatingHandler
{
    private readonly ICultureContext _ctx;
    public CultureHeadersHandler(ICultureContext ctx) => _ctx = ctx;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.AcceptLanguage.Clear();
        if (!string.IsNullOrEmpty(_ctx.Language))
            request.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue(_ctx.Language));

        SetHeader(request, "X-User-Timezone", _ctx.TimeZoneId);
        SetHeader(request, "X-User-Currency", _ctx.Currency);
        SetHeader(request, "X-Numeral-System", _ctx.NumeralSystem);

        return base.SendAsync(request, cancellationToken);
    }

    private static void SetHeader(HttpRequestMessage req, string name, string? value)
    {
        req.Headers.Remove(name);
        if (!string.IsNullOrEmpty(value))
            req.Headers.Add(name, value);
    }
}
