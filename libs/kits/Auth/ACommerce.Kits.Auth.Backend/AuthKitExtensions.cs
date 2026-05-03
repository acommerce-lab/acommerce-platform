using ACommerce.Kits.Auth.Operations;
using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.Kits.Auth.Backend;

public static class AuthKitExtensions
{
    /// <summary>
    /// يسجّل الـ Auth Kit: <see cref="IAuthUserStore"/> ينحلّ إلى
    /// <typeparamref name="TStore"/>، <see cref="AuthKitJwtConfig"/> يأخذ
    /// قيمه عبر <paramref name="jwt"/>، و <see cref="AuthController"/> يُكتشف
    /// عبر <c>AddApplicationPart</c>.
    ///
    /// <para>الاستخدام في <c>Program.cs</c>:</para>
    /// <code>
    /// // 1) auth kit (shell + controller — provider-agnostic):
    /// builder.Services.AddAuthKit&lt;MyAuthUserStore&gt;(jwt);
    /// // 2) pick a flow — common case is OTP-via-2FA:
    /// builder.Services.AddMockSmsTwoFactor();         // 2FA channel
    /// builder.Services.AddTwoFactorAsAuth();           // bridge ITwoFactorChannel→IAuthFlow
    /// </code>
    ///
    /// <para><b>المهم</b>: الـ Kit نفسه لا يسجّل <see cref="IAuthFlow"/> —
    /// التطبيق يسجّله. بدون تسجيل، تشغيل <c>AuthController</c> يفشل DI ويُفصِح
    /// عن خطأ التهيئة المبكر، أفضل من سلوك خفيّ.</para>
    /// </summary>
    public static IServiceCollection AddAuthKit<TStore>(
        this IServiceCollection services,
        AuthKitJwtConfig jwt)
        where TStore : class, IAuthUserStore
    {
        services.AddSingleton(jwt);
        services.AddScoped<IAuthUserStore, TStore>();
        services.AddControllers().AddApplicationPart(typeof(AuthController).Assembly);
        services.AddAuthKitPolicies();
        return services;
    }
}
