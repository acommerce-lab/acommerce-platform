using ACommerce.Kits.Discovery.Domain;
using ACommerce.SharedKernel.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.Kits.Discovery.Backend;

public static class DiscoveryKitExtensions
{
    public static IServiceCollection AddDiscoveryKit(this IServiceCollection services)
    {
        // CrudActionInterceptor (data interceptor) يُنشئ repository ديناميكيّاً
        // اعتماداً على EntityDiscoveryRegistry. إن لم تُسَجَّل أنواع Discovery
        // هنا، db_result يَبقى null ⇒ /cities و /amenities و /categories تُرجع
        // قائمة فارغة. هذا كان السبب المباشر لـ "قوائم المدن معطّلة" حتى مع
        // وجود البيانات في DB.
        EntityDiscoveryRegistry.RegisterEntity(typeof(DiscoveryCategory));
        EntityDiscoveryRegistry.RegisterEntity(typeof(DiscoveryRegion));
        EntityDiscoveryRegistry.RegisterEntity(typeof(DiscoveryAmenity));

        services.AddControllers().AddApplicationPart(typeof(DiscoveryController).Assembly);
        return services;
    }
}
