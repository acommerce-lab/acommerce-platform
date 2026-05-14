using ACommerce.SharedKernel.Domain.DynamicAttributes;

namespace Ashare.V3.Data.Templates;

/// <summary>
/// خُطوَة ما بَعد <see cref="DynamicAttributeHelper.BuildSnapshot"/>:
/// تَملَأ <c>DisplayValueAr</c> لِكُلّ سَمَة select/multi/bool عَبر القاموس
/// المَركَزي <see cref="V3AttributeValueTranslations"/> + تُوَحِّد عَرض
/// boolean (نَعَم/لا).
///
/// <para><b>لِماذا</b>: <c>BuildSnapshot</c> يَعتَمِد عَلى مُطابَقَة
/// <c>option.Value == raw.ToString()</c> بِحَساسِيَّة حالَة لِبِناء
/// <c>DisplayValueAr</c>. لَو الـ Value المَحفوظ في AttributesJson لا
/// يُطابِق Option.Value (تَبايُن casing، أَو لا Options مُسَجَّلَة)، يَبقى
/// <c>DisplayValueAr</c> فارِغاً والعَرض يَعود لِلراو الإنجليزي. هذه
/// الخُطوَة الاحتِياطِيَّة تُغَطّي تِلكَ الحالَة.</para>
/// </summary>
public static class V3SnapshotPostProcessor
{
    public static List<DynamicAttribute> ApplyArabicLabels(List<DynamicAttribute> snapshot)
    {
        foreach (var attr in snapshot)
        {
            if (attr.Value is null) continue;

            if (attr.Type == "select")
            {
                var rawStr = attr.Value.ToString() ?? "";
                if (string.IsNullOrEmpty(attr.DisplayValueAr) ||
                    string.Equals(attr.DisplayValueAr, rawStr, StringComparison.OrdinalIgnoreCase))
                {
                    var ar = V3AttributeValueTranslations.TryTranslate(
                        attr.Key, rawStr, attr.DisplayValue);
                    if (ar is not null) attr.DisplayValueAr = ar;
                }
            }
            else if (attr.Type == "multi" && attr.Value is IEnumerable<object> arr)
            {
                var translated = arr
                    .Select(v =>
                    {
                        var s = v?.ToString() ?? "";
                        return V3AttributeValueTranslations.TryTranslate(attr.Key, s) ?? s;
                    })
                    .ToList();
                if (translated.Count > 0)
                    attr.DisplayValueAr = string.Join("، ", translated);
            }
            else if (attr.Type == "bool")
            {
                var truthy = attr.Value switch
                {
                    bool b      => b,
                    string s    => string.Equals(s, "true", StringComparison.OrdinalIgnoreCase) || s == "1",
                    long l      => l != 0,
                    int i       => i != 0,
                    _           => false,
                };
                attr.DisplayValueAr = truthy ? "نَعَم" : "لا";
                attr.DisplayValue   = truthy ? "Yes"   : "No";
            }
        }
        return snapshot;
    }
}
