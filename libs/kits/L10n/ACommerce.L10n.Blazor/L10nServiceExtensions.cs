using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.L10n.Blazor;

public static class L10nServiceExtensions
{
    /// <summary>
    /// يسجّل <see cref="ITranslationProvider"/> و<see cref="L"/> كـ Scoped.
    /// <typeparamref name="TProvider"/> هو صنف التطبيق المعرّف بقاموسين.
    /// <typeparamref name="TLangCtx"/> هو جسر قراءة اللغة من الـ AppStore الخاص.
    /// </summary>
    public static IServiceCollection AddEmbeddedL10n<TProvider, TLangCtx>(this IServiceCollection services)
        where TProvider : class, ITranslationProvider
        where TLangCtx  : class, ILanguageContext
    {
        services.AddScoped<ILanguageContext, TLangCtx>();
        services.AddScoped<ITranslationProvider, TProvider>();
        services.AddScoped<L>();
        return services;
    }
}
