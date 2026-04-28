using ACommerce.Authentication.TwoFactor.Operations.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.Authentication.TwoFactor.Providers.Nafath.Mock.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// يسجّل قناة نفاذ تجريبية (تحقق تلقائي بعد 10 ثوانٍ) كـ <see cref="ITwoFactorChannel"/>
    /// و كـ <see cref="MockNafathTwoFactorChannel"/> مباشرةً.
    ///
    /// للإنتاج: استبدلها بـ <c>services.AddNafathTwoFactor(cfg)</c> من
    /// <c>ACommerce.Authentication.TwoFactor.Providers.Nafath</c>.
    /// </summary>
    public static IServiceCollection AddMockNafathTwoFactor(this IServiceCollection services)
    {
        services.AddSingleton<MockNafathTwoFactorChannel>();
        services.AddSingleton<ITwoFactorChannel>(sp => sp.GetRequiredService<MockNafathTwoFactorChannel>());
        return services;
    }
}
