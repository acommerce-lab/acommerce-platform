namespace ACommerce.Kits.DynamicAttributes.Operations;

/// <summary>
/// واجِهَة opt-in لِأَيّ كِيان يَحتاج خَصائِص ديناميكِيَّة. تَتَطَلَّب
/// عَمود JSON عَلى الكِيان (الـ snapshot) + مُعَرِّف نِطاق (يُخبِر
/// الكيت بِأَيّ template نُحَمِّل مِن الـ <see cref="IAttributeTemplateSource"/>).
///
/// <para><b>لِماذا opt-in</b>: ليس كُلّ كِيان يَحتاج dynamic attrs. جَداوِل
/// lookup (City, Country, AppVersion) لا تَحتاجها، فَلا داعي لِفَرض عَمود
/// JSON عَلَيها مَن <c>IBaseEntity</c>.</para>
///
/// <para><b>قاعِدَة "العَمود ⇔ الواجِهَة"</b>: الكيت لا يَفرِض اسم عَمود
/// مُعَيَّن — يَكفي أَن يُنَفِّذ الكِيان <c>AttributesJson</c> getter/setter.
/// عَمليّاً مُعظَم الكيانات تُسَمّيه <c>AttributesJson</c> لِتَوحيد المَنطِق
/// لكِنّ هذا اختيار التَطبيق لا قاعِدَة في الكيت.</para>
/// </summary>
public interface IHasDynamicAttributes
{
    /// <summary>JSON object كَ <c>{key:value}</c> لِسِمات الكِيان.
    /// <c>null</c>/فارِغ = لا قِيَم. يُعَدَّل بِواسِطَة المُعتَرِض أَو
    /// التَطبيق مُباشَرَةً.</summary>
    string? AttributesJson { get; set; }

    /// <summary>مُعَرِّف نِطاق القالَب. لِـ ProductListing = CategoryId
    /// الفِئَة؛ لِـ Profile = sentinel ثابِت لِنوع الكِيان. <c>null</c>
    /// يَعني "بِلا نِطاق" ⇒ المُعتَرِض يُهمِله.</summary>
    Guid? DynamicAttributeScopeId { get; }
}
