using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Interceptors;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ACommerce.Kits.Versions.Operations;

/// <summary>
/// المعترض الكوني لإصدار التطبيق — Pre، يعمل قبل كلّ عمليّة <b>عدا</b> عمليّات
/// <c>version.*</c> ذاتها (التي تحمل tag <see cref="VersionTagKeys.SkipVersionGate"/>).
///
/// <para>المنطق:
///   1. يقرأ <c>X-App-Version</c> + <c>X-App-Platform</c> من رؤوس الطلب الحاليّ.
///   2. يستدعي <see cref="IAppVersionGate.CheckAsync"/>.
///   3. لو الحالة <see cref="VersionStatus.Unsupported"/> يفشل العمليّة فوراً
///      بكود <see cref="VersionTagKeys.RejectionCode_Unsupported"/> + يضع الحمولة
///      الكاملة في <c>context</c> ليستطيع OpEngine تمريرها للمستجيب.
///   4. لو الحالة Latest/Active/NearSunset/Deprecated → يمرّر العمليّة بدون عوائق.
/// </para>
///
/// <para>هذا هو نفس النمط بالضبط الذي تتبعه <c>QuotaInterceptor</c> في الاشتراكات:
/// المعترض هو الـ enforcement point، لا حاجة لتعديل أيّ controller أو library
/// ليطبَّق على عمليّات إضافيّة.</para>
///
/// <para>يُسجَّل كـ singleton ويُحلّ تبعيّاته (<see cref="IAppVersionGate"/>،
/// <see cref="IHttpContextAccessor"/>) من <c>context.Services</c> الذي يحمل
/// scope الطلب الحاليّ — تجنّباً لمشاكل captured-scoped-instances.</para>
/// </summary>
public sealed class VersionGateInterceptor : IOperationInterceptor
{
    public string Name => nameof(VersionGateInterceptor);
    public InterceptorPhase Phase => InterceptorPhase.Pre;

    public bool AppliesTo(Operation op)
    {
        if (op.HasTag(VersionTagKeys.SkipVersionGate)) return false;
        if (op.Type.StartsWith("version.", StringComparison.Ordinal)) return false;
        return true;
    }

    public async Task<AnalyzerResult> InterceptAsync(OperationContext context, OperationResult? result = null)
    {
        var http = context.Services.GetService<IHttpContextAccessor>()?.HttpContext;
        if (http is null)
        {
            // طلب خارج HTTP (job داخليّ مثلاً) — لا نطبّق الفحص.
            return AnalyzerResult.Pass();
        }

        var platform = http.Request.Headers[VersionTagKeys.PlatformHeader].ToString();
        var version  = http.Request.Headers[VersionTagKeys.VersionHeader].ToString();

        if (string.IsNullOrWhiteSpace(version))
        {
            // العميل لم يُعلن إصداره — نمرّر بشكل lenient.
            return AnalyzerResult.Pass();
        }

        if (string.IsNullOrWhiteSpace(platform))
            platform = VersionTagKeys.DefaultPlatform;

        var gate = context.Services.GetService<IAppVersionGate>();
        if (gate is null)
        {
            // لم يُسجَّل تطبيق للبوّابة — نمرّر (الـ Kit ليس فعالاً في هذا الـ host).
            return AnalyzerResult.Pass();
        }

        var check = await gate.CheckAsync(platform, version, context.CancellationToken);
        context.Set("version_check_result", check);

        if (check.IsBlocked)
        {
            var logger = context.Services.GetService<ILogger<VersionGateInterceptor>>();
            logger?.LogWarning(
                "Version gate rejected request: platform={Platform}, version={Version}, status={Status}",
                platform, version, check.Status);

            return new AnalyzerResult
            {
                Passed  = false,
                Message = VersionTagKeys.RejectionCode_Unsupported,
                Data    = new Dictionary<string, object>
                {
                    ["platform"]    = platform,
                    ["version"]     = version,
                    ["status"]      = check.Status.ToString(),
                    ["latest"]      = check.Latest ?? "",
                    ["sunsetAt"]    = check.SunsetAt?.ToString("o") ?? "",
                    ["downloadUrl"] = check.DownloadUrl ?? "",
                    ["notes"]       = check.Notes ?? ""
                }
            };
        }

        return AnalyzerResult.Pass();
    }
}
