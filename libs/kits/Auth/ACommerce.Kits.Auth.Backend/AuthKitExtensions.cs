using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.Kits.Auth.Backend;

public static class AuthKitExtensions
{
    /// <summary>
    /// يسجّل الـ Auth Kit بالكامل: <see cref="IAuthUserStore"/> ينحلّ إلى
    /// <typeparamref name="TStore"/>، <see cref="AuthKitJwtConfig"/> يأخذ
    /// قيمه عبر <paramref name="jwt"/>، و <see cref="AuthController"/> يُكتشف
    /// عبر <c>AddApplicationPart</c>.
    ///
    /// <para>الاستخدام في <c>Program.cs</c>:</para>
    /// <code>
    /// builder.Services.AddAuthKit&lt;EjarAuthUserStore&gt;(
    ///     new AuthKitJwtConfig(
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
    public static IServiceCollection AddAuthKit<TStore>(
        this IServiceCollection services,
        AuthKitJwtConfig jwt)
        where TStore : class, IAuthUserStore
    {
        services.AddSingleton(jwt);
        services.AddScoped<IAuthUserStore, TStore>();

        services.AddControllers()
            .AddApplicationPart(typeof(AuthController).Assembly);

        return services;
    }
}
