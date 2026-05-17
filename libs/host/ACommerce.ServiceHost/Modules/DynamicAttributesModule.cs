using ACommerce.Kits.DynamicAttributes.Backend;
using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.ServiceHost;

public static class DynamicAttributesModule
{
    /// <summary>
    /// يُسَجِّل DynamicAttributes kit: <see cref="IAttributeTemplateSource"/>
    /// (يُنَفِّذه التَطبيق فَوق DbContext أَو in-memory) + يَضُمّ
    /// <see cref="DynamicAttributesController"/> إلى Application Parts.
    ///
    /// <code>
    /// host.UseDynamicAttributes&lt;EjarAttributeTemplateSource&gt;();
    /// </code>
    ///
    /// <para>الـ controller يُلتَقَط تِلقائيّاً عِندَ <c>UseControllers()</c>
    /// — لا حاجَة لِـ <c>AddApplicationPart(...)</c> يَدَوي.</para>
    /// </summary>
    public static ServiceHostBuilder UseDynamicAttributes<TSource>(this ServiceHostBuilder host)
        where TSource : class, IAttributeTemplateSource
    {
        host.Builder.Services.AddScoped<IAttributeTemplateSource, TSource>();
        host.ExtraControllerAssemblies.Add(typeof(DynamicAttributesController).Assembly);
        return host;
    }
}
