namespace ACommerce.Kits.Taxonomy.Operations;

/// <summary>
/// واجِهَة opt-in لِأَيّ كِيان يُريد رِبط <b>Guid مُباشِر</b> بِعُقدَة
/// شَجَرَة. <b>اختياري</b>: التَّوصِيَة الأَساسِيَّة هي ربط slug عَبر حَقل
/// مَوجود (مَثَل <c>IListing.PropertyType</c>) — الـ Listings kit لا تَعرِف
/// عَن Taxonomy، وهذا أَنظَف decoupling.
///
/// <para>هذه الواجِهَة لِلتَطبيقات الَّتي تَحتاج FK قَوي + integrity مَن
/// DB. Listings kit لا تَستَخدِمها.</para>
/// </summary>
public interface IHasTaxonomyNode
{
    Guid? TaxonomyNodeId { get; }
}
