using ACommerce.Compositions.Core;
using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Interceptors;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ACommerce.Compositions.Auth.WithSmsOtp;

/// <summary>
/// Bundle: تدقيق ناجح/فاشل لعمليّات تسجيل الدخول (<c>auth.signin</c>) عبر
/// OTP. لا يضيف منطق دومين — مجرّد سطر log منظَّم لكلّ محاولة، يكفي لتعقّب
/// الاحتيال أو حسابات brute-force دون أن يلمس Auth kit.
///
/// <para>هذا أبسط ما يقدّمه composition: دون تعديل أيّ kit، نُلصِق سلوك
/// cross-cutting (تدقيق، rate-limit، إخطار إداريّ، …) من خارجها.</para>
/// </summary>
public sealed class AuthOtpAuditBundle : IInterceptorBundle
{
    public string Name => "Auth.WithSmsOtp.Audit";
    public IEnumerable<Type> InterceptorTypes => new[] { typeof(AuthSigninAuditInterceptor) };
}

public sealed class AuthSigninAuditInterceptor : IOperationInterceptor
{
    private readonly ILogger<AuthSigninAuditInterceptor> _log;

    public string Name => "Auth.SigninAudit";
    public InterceptorPhase Phase => InterceptorPhase.Post;

    public AuthSigninAuditInterceptor(ILogger<AuthSigninAuditInterceptor> log) { _log = log; }

    public bool AppliesTo(Operation op) => op.Type == "auth.signin";

    public Task<AnalyzerResult> InterceptAsync(OperationContext ctx, OperationResult? result = null)
    {
        try
        {
            // From party → "Issuer:authenticator_name"؛ نلتقط authenticator
            // من الـ tags.
            var authTag = ctx.Operation.Tags.FirstOrDefault(t => t.Key == "authenticator");
            var authenticator = string.IsNullOrEmpty(authTag.Key) ? "?" : authTag.Value;

            // To party → user
            var user = ctx.Operation.Parties
                .FirstOrDefault(p => p.Tags.Any(t => t.Key == "direction" && t.Value == "credit"));
            var userId = user?.Identity ?? "?";

            var success = result?.Success ?? false;
            if (success)
                _log.LogInformation("auth.signin OK: user={User} via={Auth}", userId, authenticator);
            else
                _log.LogWarning("auth.signin FAIL: user={User} via={Auth} reason={Reason}",
                    userId, authenticator, result?.ErrorMessage ?? "unknown");
        }
        catch (Exception ex)
        {
            _log.LogDebug(ex, "Auth.SigninAudit: تجاهل خطأ تدقيق غير قاتل");
        }
        return Task.FromResult(AnalyzerResult.Pass());
    }
}
