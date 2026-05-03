using ACommerce.Kits.Support.Operations;
using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.Kits.Support.Backend;

/// <summary>
/// تسجيل Support kit في DI. التطبيق يحقن <typeparamref name="TStore"/> الذي
/// يُنفِّذ <see cref="ISupportStore"/> ويعرف شكل DB الفعليّ.
///
/// <para>المتطلّبات المسبقة في DI قبل استدعاء هذه الدالّة:
/// <list type="bullet">
///   <item><c>OpEngine</c> + <c>OperationInterceptorRegistry</c> (عبر <c>AddOpEngine</c>).</item>
///   <item>Chat kit (<c>AddChatKit</c>) — Support's Reply يستهلك
///         <c>IChatStore.AppendMessageAsync</c> داخل Execute body.</item>
///   <item>JWT auth — الـ controller يقرأ <c>user_id</c> claim للـ scoping.</item>
/// </list></para>
/// </summary>
public static class SupportKitExtensions
{
    public static IServiceCollection AddSupportKit<TStore>(
        this IServiceCollection services,
        Action<SupportKitOptions>? configure = null)
        where TStore : class, ISupportStore
    {
        var options = new SupportKitOptions();
        configure?.Invoke(options);
        services.AddSingleton(options);
        services.AddScoped<ISupportStore, TStore>();
        services.AddControllers().AddApplicationPart(typeof(SupportController).Assembly);
        services.AddSupportKitPolicies();
        return services;
    }
}
