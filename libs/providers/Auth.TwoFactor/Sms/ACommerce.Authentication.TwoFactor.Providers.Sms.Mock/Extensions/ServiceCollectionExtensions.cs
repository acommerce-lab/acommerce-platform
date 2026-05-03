using ACommerce.Authentication.TwoFactor.Operations.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.Authentication.TwoFactor.Providers.Sms.Mock.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// يسجّل قناة OTP تجريبية (رمز ثابت 123456) كـ <see cref="ITwoFactorChannel"/> و
    /// كـ <see cref="MockSmsTwoFactorChannel"/> مباشرةً.
    ///
    /// للإنتاج: استبدلها بـ <c>services.AddSmsTwoFactor()</c> من
    /// <c>ACommerce.Authentication.TwoFactor.Providers.Sms</c>.
    /// </summary>
    public static IServiceCollection AddMockSmsTwoFactor(this IServiceCollection services)
    {
        services.AddSingleton<MockSmsTwoFactorChannel>();
        services.AddSingleton<ITwoFactorChannel>(sp => sp.GetRequiredService<MockSmsTwoFactorChannel>());
        return services;
    }
}
