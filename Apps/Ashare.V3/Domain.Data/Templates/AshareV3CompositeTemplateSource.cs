using ACommerce.Kits.DynamicAttributes.Backend;
using ACommerce.SharedKernel.Domain.DynamicAttributes;

namespace Ashare.V3.Data.Templates;

/// <summary>
/// مَصدَر قَوالِب مُرَكَّب لِـ V3:
/// <list type="number">
///   <item>سِمات بروفايل sentinel ⇒ يُفَوَّض لِـ ProductionAttributeTemplateSource.</item>
///   <item>سِمات روممَت (<c>roommate_has</c>/<c>roommate_wants</c>) ⇒
///         قالَب hardcoded — لا تَوجَد في جَدول الإنتاج.</item>
///   <item>أَيّ scope آخَر (CategoryId إنتاج فِعلي) ⇒ يُفَوَّض لِـ
///         ProductionAttributeTemplateSource.</item>
/// </list>
///
/// <para>الـ scopeId المُشتَقّ مَن slug "roommate_has"/"roommate_wants"
/// يَتَطابَق مَع Guids في <see cref="AshareV3TaxonomyStore"/>.</para>
/// </summary>
public sealed class AshareV3CompositeTemplateSource : IAttributeTemplateSource
{
    private readonly ProductionAttributeTemplateSource _prod;
    public AshareV3CompositeTemplateSource(ProductionAttributeTemplateSource prod) => _prod = prod;

    // الـ Guids ثابِتَة — نَفسها في <see cref="AshareV3TaxonomyStore"/>.
    private static readonly Guid RoommateHas   = Guid.Parse("0a01a01a-0a01-0a01-0a01-0a01000a01a2");
    private static readonly Guid RoommateWants = Guid.Parse("0a01a01a-0a01-0a01-0a01-0a01000a01a3");

    public Task<AttributeTemplate?> BuildForScopeAsync(Guid scopeId, CancellationToken ct)
    {
        if (scopeId == RoommateHas)
            return Task.FromResult<AttributeTemplate?>(RoommateHasTemplate);
        if (scopeId == RoommateWants)
            return Task.FromResult<AttributeTemplate?>(RoommateWantsTemplate);

        // كُلّ ما عَدا ذلك ⇒ الإنتاج (asharedb).
        return _prod.BuildForScopeAsync(scopeId, ct);
    }

    // ─── قَوالِب الروممَت ─────────────────────────────────────────────
    // "عَنده سَكَن" — يَنشُر غُرفَة/مَكاناً في شَقَّة، فَيَحتاج وَصف
    // الإيجار + المُتَطَلَّبات في الشَريك.
    private static readonly AttributeTemplate RoommateHasTemplate = new()
    {
        Fields = new()
        {
            new() { Key = "RoomPrice",      Label = "سِعر الغُرفَة",       LabelAr = "سِعر الغُرفَة",      Type = "number", SortOrder = 1 },
            new() { Key = "BedroomShare",   Label = "تَشارُك الغُرفَة",    LabelAr = "تَشارُك الغُرفَة",   Type = "select", SortOrder = 2,
                    Options = new() {
                        new() { Value = "private",  Label = "Private",  LabelAr = "خاصَّة" },
                        new() { Value = "shared",   Label = "Shared",   LabelAr = "مُشتَرَكَة" }
                    }},
            new() { Key = "GenderPref",     Label = "تَفضيل الجِنس",       LabelAr = "تَفضيل الجِنس",     Type = "select", SortOrder = 3,
                    Options = new() {
                        new() { Value = "male",   Label = "Male",   LabelAr = "ذَكَر" },
                        new() { Value = "female", Label = "Female", LabelAr = "أُنثى" },
                        new() { Value = "any",    Label = "Any",    LabelAr = "أَيّ" }
                    }},
            new() { Key = "Smoking",        Label = "التَدخين",            LabelAr = "التَدخين",          Type = "select", SortOrder = 4,
                    Options = new() {
                        new() { Value = "allowed",     Label = "Allowed",     LabelAr = "مَسموح" },
                        new() { Value = "not_allowed", Label = "Not allowed", LabelAr = "غَير مَسموح" }
                    }},
            new() { Key = "Furnished",      Label = "التَّأثيث",            LabelAr = "التَّأثيث",         Type = "bool",   SortOrder = 5 },
            new() { Key = "Parking",        Label = "مَوقِف سَيّارَة",     LabelAr = "مَوقِف سَيّارَة",   Type = "bool",   SortOrder = 6 },
            new() { Key = "Wifi",           Label = "إنتَرنِت",             LabelAr = "إنتَرنِت",          Type = "bool",   SortOrder = 7 },
            new() { Key = "AvailableFrom",  Label = "مُتاحَة مِن",          LabelAr = "مُتاحَة مِن",       Type = "date",   SortOrder = 8 },
        }
    };

    // "يَدور سَكَن" — مَعلومات الباحِث وَ مُتَطَلَّباته.
    private static readonly AttributeTemplate RoommateWantsTemplate = new()
    {
        Fields = new()
        {
            new() { Key = "Budget",         Label = "المِيزانِيَّة",        LabelAr = "المِيزانِيَّة",     Type = "number", SortOrder = 1 },
            new() { Key = "PreferredArea",  Label = "المَنطِقَة المُفَضَّلَة", LabelAr = "المَنطِقَة المُفَضَّلَة", Type = "text", SortOrder = 2 },
            new() { Key = "Gender",         Label = "الجِنس",              LabelAr = "الجِنس",            Type = "select", SortOrder = 3,
                    Options = new() {
                        new() { Value = "male",   Label = "Male",   LabelAr = "ذَكَر" },
                        new() { Value = "female", Label = "Female", LabelAr = "أُنثى" }
                    }},
            new() { Key = "Age",            Label = "العُمر",               LabelAr = "العُمر",            Type = "number", SortOrder = 4 },
            new() { Key = "Occupation",     Label = "المِهنَة",             LabelAr = "المِهنَة",          Type = "text",   SortOrder = 5 },
            new() { Key = "Smoker",         Label = "مُدَخِّن",             LabelAr = "مُدَخِّن",          Type = "bool",   SortOrder = 6 },
            new() { Key = "MoveInBy",       Label = "تاريخ الانتِقال",      LabelAr = "تاريخ الانتِقال",   Type = "date",   SortOrder = 7 },
        }
    };
}
