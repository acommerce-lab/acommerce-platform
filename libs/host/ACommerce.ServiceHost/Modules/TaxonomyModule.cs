using ACommerce.Kits.Taxonomy.Backend;
using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.ServiceHost;

public static class TaxonomyModule
{
    /// <summary>
    /// يُسَجِّل Taxonomy kit: <see cref="ITaxonomyStore"/> + يَضُمّ
    /// <see cref="TaxonomyController"/> إلى Application Parts.
    ///
    /// <code>
    /// host.UseTaxonomy&lt;EjarTaxonomyStore&gt;();
    /// </code>
    /// </summary>
    public static ServiceHostBuilder UseTaxonomy<TStore>(this ServiceHostBuilder host)
        where TStore : class, ITaxonomyStore
    {
        host.Builder.Services.AddScoped<ITaxonomyStore, TStore>();
        host.ExtraControllerAssemblies.Add(typeof(TaxonomyController).Assembly);
        return host;
    }
}
