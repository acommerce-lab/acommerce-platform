using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.Kits.Auth.Sms.Backend;

public static class AuthSmsKitExtensions
{
    /// <summary>
    /// يسجّل الـ Auth Kit بالكامل: <see cref="IAuthUserStore"/> ينحلّ إلى
    /// <typeparamref name="TStore"/>، <see cref="AuthSmsKitJwtConfig"/> يأخذ
    /// قيمه عبر <paramref name="jwt"/>، و <see cref="AuthController"/> يُكتشف
    /// عبر <c>AddApplicationPart</c>.
    ///
    /// <para>الاستخدام في <c>Program.cs</c>:</para>
    /// <code>
    /// builder.Services.AddSmsAuthKit&lt;EjarAuthUserStore&gt;(
    ///     new AuthSmsKitJwtConfig(
    ///         Secret:    cfg["JWT:SecretKey"]!,
    ///         Issuer:    cfg["JWT:Issuer"]!,
    ///         Audience:  cfg["JWT:Audience"]!,
    ///         Role:      "provider",
    ///         PartyKind: "Provider"));
    /// </code>
    ///
    /// <para>شرط مسبق: <c>AddMockSmsTwoFactor()</c> أو نسخة الإنتاج
    /// مسجَّلة (يحقن <c>ITwoFactorChannel</c>).</para>
    /// </summary>
    public static IServiceCollection AddSmsAuthKit<TStore>(
        this IServiceCollection services,
        AuthSmsKitJwtConfig jwt)
        where TStore : class, IAuthUserStore
    {
        services.AddSingleton(jwt);
        services.AddScoped<IAuthUserStore, TStore>();

        services.AddControllers()
            .AddApplicationPart(typeof(AuthController).Assembly);

        return services;
    }
}
