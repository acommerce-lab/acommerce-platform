using ACommerce.Kits.Versions.Operations;

namespace ACommerce.Kits.Versions.Templates;

/// <summary>
/// <see cref="DelegatingHandler"/> يضيف رؤوس <c>X-App-Version</c> و
/// <c>X-App-Platform</c> لكلّ طلب صادر. يُسجَّل على الـ <c>HttpClient</c>
/// المسمّى الذي يستخدمه التطبيق للاتصال بخدمته الخلفيّة.
///
/// <para>الاستخدام في Program.cs:
/// <code>
///   builder.Services.AddSingleton(new AppVersionInfo(
///       Platform: "web", Version: "1.2.0"));
///   builder.Services.AddTransient&lt;AppVersionHeadersHandler&gt;();
///   builder.Services.AddHttpClient("ejar", c =&gt; ...)
///                   .AddHttpMessageHandler&lt;AppVersionHeadersHandler&gt;();
/// </code></para>
/// </summary>
public sealed class AppVersionHeadersHandler : DelegatingHandler
{
    private readonly AppVersionInfo _info;

    public AppVersionHeadersHandler(AppVersionInfo info) => _info = info;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!request.Headers.Contains(VersionTagKeys.VersionHeader))
            request.Headers.Add(VersionTagKeys.VersionHeader, _info.Version);
        if (!request.Headers.Contains(VersionTagKeys.PlatformHeader))
            request.Headers.Add(VersionTagKeys.PlatformHeader, _info.Platform);
        return base.SendAsync(request, cancellationToken);
    }
}

/// <summary>
/// إصدار التطبيق وقت التشغيل — يُحقن singleton ليقرأه كلٌّ من الرأس handler ومن
/// الـ <see cref="VersionState"/> عند بدء الفحص.
/// </summary>
public sealed record AppVersionInfo(string Platform, string Version);
