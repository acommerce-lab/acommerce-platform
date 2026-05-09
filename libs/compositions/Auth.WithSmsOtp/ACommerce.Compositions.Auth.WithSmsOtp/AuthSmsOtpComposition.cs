using ACommerce.Authentication.TwoFactor.Operations.Abstractions;
using ACommerce.Compositions.Core;
using ACommerce.Kits.Auth.Operations;

namespace ACommerce.Compositions.Auth.WithSmsOtp;

/// <summary>
/// تركيب: Auth + 2FA عبر SMS OTP. الـ Auth kit و 2FA kit يظلّان نقيَّين
/// ولا يعرفان بعضهما — التركيب يربطهما بشكل خارجيّ:
///
/// <list type="number">
///   <item><see cref="RequiredKits"/> يفرض وجودهما في DI قبل الإقلاع
///         (<c>IAuthUserStore</c> + <c>ITwoFactorChannel</c>) فيُكشف أيّ
///         نسيان فوراً بدل runtime errors.</item>
///   <item><see cref="AuthOtpAuditBundle"/> يلتقط <c>auth.signin</c> ويُسجِّل
///         كلّ محاولة بشكل بنيويّ — أساس لـ rate-limit / fraud detection
///         لاحقاً (يُضاف bundles إضافيّة بدون لمس kit).</item>
/// </list>
///
/// الاستهلاك:
/// <code>
/// services.AddAuthKit(...);
/// services.AddTwoFactorKit(...);          // ITwoFactorChannel implementation
/// services.AddTwoFactorAsAuth(...);       // الجسر بين 2FA و Auth
/// services.AddComposition&lt;AuthSmsOtpComposition&gt;();
/// </code>
/// </summary>
public sealed class AuthSmsOtpComposition : ICompositionDescriptor
{
    public string Name => "Auth + SMS OTP";

    public IEnumerable<Type> RequiredKits => new[]
    {
        typeof(IAuthUserStore),
        typeof(ITwoFactorChannel),
    };

    public IEnumerable<IInterceptorBundle> Bundles => new[]
    {
        (IInterceptorBundle)new AuthOtpAuditBundle(),
    };
}
