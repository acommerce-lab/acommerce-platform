using System.Globalization;
using System.Reflection;
using System.Resources;
using ACommerce.L10n.Blazor;
using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.Compositions.Customer.L10n.Resx;

/// <summary>
/// تَنفيذ <see cref="ITranslationProvider"/> يَقرأ مِن <c>ResourceManager</c>
/// (.resx + .{lang}.resx) في assembly مُعَيَّن. التَطبيق يُسَجِّله:
/// <code>
/// services.AddResxTranslationProvider(typeof(MyTemplate).Assembly,
///     baseName: "MyTemplate.Resources.Strings");
/// </code>
///
/// <para>سابِقاً (V1)، هذا الكود كانَ <c>EjarTranslationProvider</c> ضِمن
/// قالَب Customer.Marketplace. تَوسيعَه composition يَجعَله مُتاحاً لِأيّ
/// تَطبيق يَحوي .resx — لا يَحتاج تَطبيق Customer.Marketplace كَ dependency.</para>
/// </summary>
public sealed class ResxTranslationProvider : ITranslationProvider
{
    private readonly ResourceManager _rm;

    public ResxTranslationProvider(Assembly assembly, string baseName)
    {
        _rm = new ResourceManager(baseName, assembly);
    }

    public string Translate(string key, string language)
    {
        var culture = string.IsNullOrEmpty(language)
            ? CultureInfo.InvariantCulture
            : CultureInfo.GetCultureInfo(language);
        return _rm.GetString(key, culture) ?? key;
    }
}

public static class ResxTranslationServiceCollectionExtensions
{
    /// <summary>
    /// يُسَجِّل <see cref="ResxTranslationProvider"/> كَـ <c>ITranslationProvider</c>
    /// scoped — يُربَط بِـ assembly الـ resx + base name. التَطبيق يَستَعمِله
    /// عَبر <c>L</c> (مِن L10n.Blazor) دون لَمس الـ ResourceManager.
    /// </summary>
    public static IServiceCollection AddResxTranslationProvider(
        this IServiceCollection services,
        Assembly assembly,
        string baseName)
    {
        services.AddScoped<ITranslationProvider>(_ => new ResxTranslationProvider(assembly, baseName));
        return services;
    }
}
