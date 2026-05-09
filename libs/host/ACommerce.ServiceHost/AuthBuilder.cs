using ACommerce.Authentication.TwoFactor.Providers.Sms.Mock.Extensions;
using ACommerce.Kits.Auth.TwoFactor.AsAuth;
using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.ServiceHost;

/// <summary>
/// builder Auth — يَكشف ميزات إضافيّة فوق الـ JWT الأساسيّ.
/// 2FA حاليّاً، lockout/passwordless لاحقاً.
///
/// <para>الإستخدام:</para>
/// <code>
/// kits.AddAuth&lt;TStore&gt;(jwt, auth =&gt; auth
///     .AddTwoFactor(tf =&gt; tf.UseMockSmsProvider()));
/// </code>
/// </summary>
public sealed class AuthBuilder
{
    public IServiceCollection Services { get; }

    internal AuthBuilder(IServiceCollection services) => Services = services;

    /// <summary>
    /// يُفعِّل المصادقة الثنائيّة كـ <c>IAuthFlow</c> فوق Auth kit.
    /// <paramref name="configure"/> يَختار الـ provider (Mock/SMS الحقيقيّة/Email/Nafath…).
    ///
    /// <para>2FA بدون Auth ممنوع بنيويّاً — هذا method موجود فقط على
    /// <see cref="AuthBuilder"/> الذي يتطلّب <c>AddAuth</c> أوّلاً.</para>
    /// </summary>
    public AuthBuilder AddTwoFactor(Action<TwoFactorBuilder> configure)
    {
        var tf = new TwoFactorBuilder(Services);
        configure(tf);
        if (!tf.HasProvider)
            throw new InvalidOperationException(
                "AddTwoFactor requires a provider — call e.g. tf.UseMockSmsProvider() inside the configure block.");

        // الجسر بين IAuthFlow و ITwoFactorChannel — يُسجَّل بعد تسجيل الـ provider.
        Services.AddTwoFactorAsAuth();
        return this;
    }
}

/// <summary>
/// builder الـ 2FA provider. مزوّد واحد لكلّ تطبيق (الكيت يفترض channel
/// واحد فعّال). تطبيقات تَدعم channels متعدّدة (SMS + Email للاختيار)
/// تَسجِّل الـ resolution منطقها يدوياً ولا تستعمل هذا الـ builder.
/// </summary>
public sealed class TwoFactorBuilder
{
    private readonly IServiceCollection _services;
    internal bool HasProvider { get; private set; }

    internal TwoFactorBuilder(IServiceCollection services) => _services = services;

    /// <summary>Mock SMS — كود ثابت <c>123456</c>. مفيد في dev/testing.</summary>
    public TwoFactorBuilder UseMockSmsProvider()
    {
        _services.AddMockSmsTwoFactor();
        HasProvider = true;
        return this;
    }

    // مساحة لـ providers مستقبليّة:
    //   public TwoFactorBuilder UseSmsProvider<T>() where T : ITwoFactorChannel
    //   public TwoFactorBuilder UseEmailProvider(...)
    //   public TwoFactorBuilder UseNafathProvider(...)
}
