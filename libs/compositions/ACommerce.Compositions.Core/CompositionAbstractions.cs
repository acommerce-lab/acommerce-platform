using ACommerce.OperationEngine.Interceptors;
using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.Compositions.Core;

/// <summary>
/// Bundle = إعلان عن مجموعة أنواع <see cref="IOperationInterceptor"/>
/// concrete التي ينتجها التركيب. مجرّد قائمة <see cref="Type"/> — التركيب
/// يُسجِّل كلّاً منها كـ Singleton عبر DI بشكل عاديّ، فالحقن (constructor)
/// يجلب الـ services اللازمة (ومنها IServiceProvider لو احتاج المعترض
/// خدمات Scoped لحلّها لاحقاً عبر CreateScope).
///
/// <para>الـ bundle يُعرَّف خارج الـ kits (في <c>libs/compositions/</c>)
/// فيُحقَن interceptors **من خارج** المكتبات المعنيّة، حسب نمط OAM في
/// <c>docs/COMPOSITION-MODEL.md</c>.</para>
/// </summary>
public interface IInterceptorBundle
{
    /// <summary>اسم تشخيصيّ — يظهر في التتبّع.</summary>
    string Name { get; }

    /// <summary>أنواع المعترضات الـ concrete. يجب أن تكون كلّ منها قابلة
    /// للحلّ عبر DI (constructor public) ومنفّذة <see cref="IOperationInterceptor"/>.</summary>
    IEnumerable<Type> InterceptorTypes { get; }
}

/// <summary>
/// Composition = "تركيب" = ضمّ kits (و/أو compositions أخرى) عبر
/// <see cref="IInterceptorBundle"/> دون أن تعرف الـ kits بعضها.
///
/// <para>التركيب نفسه قابل للتركيب: <see cref="Subcompositions"/>
/// تجعل من تركيبَين مكوَّناً واحداً. مَيِّز هذا عن "kit" الذي يحوي محاسبةً
/// نقيّةً لمجال بعينه.</para>
/// </summary>
public interface ICompositionDescriptor
{
    string Name { get; }
    /// <summary>الـ types التي يجب أن تكون مسجَّلة في DI — مثلاً
    /// <c>typeof(IChatStore)</c>. تُفحَص عند <c>AddComposition&lt;T&gt;</c>
    /// فيُكشف نقص الاعتماد قبل أوّل طلب.</summary>
    IEnumerable<Type> RequiredKits => [];
    IEnumerable<IInterceptorBundle> Bundles => [];
    IEnumerable<ICompositionDescriptor> Subcompositions => [];
}

public static class CompositionExtensions
{
    /// <summary>
    /// يُسجِّل التركيب في DI: يفحص الاعتمادات، يحقن كلّ interceptors من
    /// كلّ Bundle (مع subcompositions recursively).
    /// </summary>
    public static IServiceCollection AddComposition<T>(this IServiceCollection services)
        where T : ICompositionDescriptor, new()
    {
        var descriptor = new T();
        return services.AddCompositionDescriptor(descriptor);
    }

    public static IServiceCollection AddCompositionDescriptor(
        this IServiceCollection services, ICompositionDescriptor descriptor)
    {
        // ① subcompositions أوّلاً — إن كان X يضمّ Y، فإسقاط Y لا يجب أن
        // يكسر X. الـ DI يحلّ التكرار تلقائياً (singleton يُسجَّل مرّة).
        foreach (var sub in descriptor.Subcompositions)
            services.AddCompositionDescriptor(sub);

        // ② تحقّق الاعتمادات. ServiceProvider لم يُبنَ بعد — نفحص الـ
        // ServiceCollection الخامة.
        foreach (var kitType in descriptor.RequiredKits)
        {
            var present = services.Any(s => s.ServiceType == kitType);
            if (!present)
                throw new InvalidOperationException(
                    $"Composition '{descriptor.Name}' requires {kitType.FullName} — " +
                    $"register the kit (e.g. AddChatKit / AddRealtimeKit) before " +
                    $"AddComposition<{descriptor.GetType().Name}>().");
        }

        // ③ سجّل كلّ صنف interceptor مُعلن في الـ Bundle كـ Singleton عبر
        // DI القياسيّ. الـ ctor يستلم اعتماداته بالطرق المعتادة. للـ Scoped
        // services يحقن المعترض IServiceProvider ويُنشئ scope لكلّ استدعاء.
        foreach (var bundle in descriptor.Bundles)
        {
            foreach (var interceptorType in bundle.InterceptorTypes)
            {
                if (!typeof(IOperationInterceptor).IsAssignableFrom(interceptorType))
                    throw new InvalidOperationException(
                        $"Bundle '{bundle.Name}' declared {interceptorType.FullName} which does not implement IOperationInterceptor.");
                services.AddSingleton(interceptorType);
                services.AddSingleton(typeof(IOperationInterceptor), sp =>
                    sp.GetRequiredService(interceptorType));
            }
        }
        return services;
    }
}
