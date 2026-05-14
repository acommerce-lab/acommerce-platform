using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ACommerce.ServiceHost;

public static class ControllersModule
{
    /// <summary>
    /// يُسجِّل Controllers + EndpointsApiExplorer + Swagger.
    /// Swagger middleware يُفعَّل في <see cref="ServiceHostExtensions.UseACommerceServiceHost"/>
    /// تلقائيّاً في بيئة التطوير.
    /// </summary>
    public static ServiceHostBuilder UseControllers(this ServiceHostBuilder host)
    {
        var s = host.Builder.Services;
        var mvc = s.AddControllers();
        // تَلتَقِط controllers مَن كُلّ assembly سَجَّلَتها modules أُخرى
        // (UseDynamicAttributes/UseTaxonomy/UseMarketplaceHomeBackend) — لا
        // حاجَة لِـ <c>AddApplicationPart(...)</c> يَدَوي في Program.cs.
        foreach (var asm in host.ExtraControllerAssemblies)
            mvc.AddApplicationPart(asm);

        s.AddEndpointsApiExplorer();
        s.AddSwaggerGen();

        host.ConfigureApp(app =>
        {
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }
            app.MapControllers();
        });
        return host;
    }
}
