using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.ClientHost.Preferences;

public static class UiPreferencesExtensions
{
    /// <summary>
    /// يُسَجِّل <see cref="IUiPreferences"/> + <see cref="LocalStorageUiPersistence"/>.
    /// التَطبيق:
    /// <code>
    /// services.AddUiPreferences&lt;DefaultUiPreferences&gt;("ejar.ui");
    /// // أَو لِنَوع مَخصَّص:
    /// services.AddUiPreferences&lt;EjarUiPreferences&gt;("ejar.ui");
    /// </code>
    /// </summary>
    public static IServiceCollection AddUiPreferences<TImpl>(
        this IServiceCollection services,
        string storageKey)
        where TImpl : class, IUiPreferences
    {
        services.AddSingleton(new UiPreferencesOptions(storageKey));
        services.AddScoped<IUiPreferences, TImpl>();
        services.AddScoped<LocalStorageUiPersistence>();
        return services;
    }

    public static IServiceCollection AddUiPreferences(this IServiceCollection services, string storageKey)
        => AddUiPreferences<DefaultUiPreferences>(services, storageKey);
}
