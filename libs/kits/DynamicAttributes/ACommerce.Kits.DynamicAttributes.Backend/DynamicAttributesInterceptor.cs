using ACommerce.Kits.DynamicAttributes.Operations;
using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Interceptors;
using ACommerce.SharedKernel.Domain.DynamicAttributes;
using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.Kits.DynamicAttributes.Backend;

/// <summary>
/// مُعتَرِض يُحَمِّل snapshot سِمات الكِيان داخِل
/// <c>OperationContext.Items["dyn_snapshot"]</c> لِأَيّ عَمَلِيَّة
/// مَوسومَة بِـ <see cref="DynamicAttributeTagKeys.HydrateSnapshot"/>.
///
/// <para><b>كَيف يُستَخدَم</b> — في عَمَلِيَّة قِراءَة (GET listing/profile):</para>
/// <code>
/// Entry.Create("listings.get")
///   .Tag(DynamicAttributeTagKeys.HydrateSnapshot, "1")
///   .Execute(async ctx =>
///   {
///       var entity = await store.GetAsync(id, ctx.CancellationToken);
///       ctx.Items["dyn_entity"] = entity;   // المُعتَرِض يَلتَقِطها
///       ...
///   });
/// </code>
///
/// <para>بَعد التَّنفيذ، المُعتَرِض يُحَوِّل الكِيان (إن كان
/// <see cref="IHasDynamicAttributes"/>) إلى snapshot عَبر
/// <see cref="AttributeSnapshotBuilder"/> ويَضَعه في
/// <c>Items["dyn_snapshot"]</c>. الـ controller يَقرَؤه ويُضيفه
/// لِـ envelope.Data.</para>
///
/// <para>اختياري — التَطبيقات الَّتي تَستَخدِم enricher مُخَصَّص
/// (كَ AshareV3ListingDetailEnricher) لا تَحتاج المُعتَرِض. التَطبيقات
/// الجَديدَة الَّتي تُريد سِمات مَجّاناً بِلا enricher تُسَجِّله مَرَّة
/// واحِدَة في <c>ServiceHost</c>.</para>
/// </summary>
public sealed class DynamicAttributesInterceptor : IOperationInterceptor
{
    public string Name => "dynamic_attributes_hydrate";
    public InterceptorPhase Phase => InterceptorPhase.Post;

    public bool AppliesTo(Operation op)
        => op.HasTag(DynamicAttributeTagKeys.HydrateSnapshot.Name);

    public async Task<AnalyzerResult> InterceptAsync(
        OperationContext context, OperationResult? result = null)
    {
        var source = context.Services.GetService<IAttributeTemplateSource>();
        if (source is null) return AnalyzerResult.Pass();

        // نَقبَل الكِيان عَبر مَفتاحَين:
        // - Items["dyn_entity"]  ← الـ Execute body يَضَعها صَراحَةً
        // - WithEntity<IHasDynamicAttributes>  ← لَو الـ kit مُعتَمِد عَلى
        //   typed entity flow.
        IHasDynamicAttributes? entity = null;
        if (context.Items.TryGetValue("dyn_entity", out var raw) && raw is IHasDynamicAttributes e)
            entity = e;
        else
            entity = context.Entity<IHasDynamicAttributes>();

        if (entity is null) return AnalyzerResult.Pass();

        var snapshot = await AttributeSnapshotBuilder.BuildForAsync(
            source, entity, context.CancellationToken);
        context.Items["dyn_snapshot"] = snapshot;

        return AnalyzerResult.Pass();
    }
}

public static class DynamicAttributesInterceptorExtensions
{
    /// <summary>يُسَجِّل المُعتَرِض في DI. التَطبيق يَستَدعيه مَرَّة في Program.cs.</summary>
    public static IServiceCollection AddDynamicAttributesInterceptor(this IServiceCollection services)
    {
        services.AddSingleton<IOperationInterceptor, DynamicAttributesInterceptor>();
        return services;
    }
}
