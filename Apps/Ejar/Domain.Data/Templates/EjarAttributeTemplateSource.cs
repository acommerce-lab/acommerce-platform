using ACommerce.Kits.DynamicAttributes.Backend;
using ACommerce.SharedKernel.Domain.DynamicAttributes;
using Ejar.Api.Data;
using Ejar.Domain;

namespace Ejar.Api.Data.Templates;

/// <summary>
/// مَصدَر قَوالِب Ejar — يَبني <see cref="AttributeTemplate"/> في الذاكِرَة.
/// يُمَيِّز ثَلاث حالات:
///
/// <list type="number">
///   <item><b>Profile scope</b>: قالَب Bio/Occupation/Nationality/Languages.</item>
///   <item><b>Listing scope</b>: scopeId مُشتَقّ مَن PropertyType slug.
///         نَعكِس الـ slug ⇒ نَستَخرِج kind مَن EjarSeed.Categories ⇒
///         نَعيد القالَب المُناسِب (Realty/Vehicle/Event/Camps).</item>
///   <item><b>Unknown</b>: قالَب universal فارِغ.</item>
/// </list>
///
/// <para><b>لِماذا per-kind</b>: حَلّ مَشكَلَة "إعلان باص يَطلُب
/// عَدَد غُرَف". القالَب يَختَلِف حَسب الـ kind، لا يَوجَد قالَب واحِد.</para>
///
/// <para>الـ scopeId reverse-lookup: نَبني <see cref="_scopeToKind"/>
/// مَرَّة واحِدَة عِندَ static ctor — يَحوي Guid لِكُلّ slug مَعروف.</para>
/// </summary>
public sealed class EjarAttributeTemplateSource : IAttributeTemplateSource
{
    /// <summary>خَريطَة <c>scopeId (مُشتَقّ مَن slug) → kind</c>. تُبنى
    /// مَرَّة عِندَ تَحميل الصَنف.</summary>
    private static readonly Dictionary<Guid, string> _scopeToKind = BuildScopeMap();

    private static Dictionary<Guid, string> BuildScopeMap()
    {
        var map = new Dictionary<Guid, string>();
        foreach (var c in EjarSeed.Categories)
            map[EjarListingScopes.DeriveScopeId(c.Id)] = c.Kind;
        return map;
    }

    public Task<AttributeTemplate?> BuildForScopeAsync(Guid scopeId, CancellationToken ct)
    {
        if (scopeId == EjarProfileAttributes.ScopeId)
            return Task.FromResult<AttributeTemplate?>(BuildFromSeeds(EjarProfileAttributes.Defaults));

        // Listing scope: نَكتَشِف الـ kind مَن scopeId ⇒ نَعيد القالَب المُلائِم.
        if (_scopeToKind.TryGetValue(scopeId, out var kind))
        {
            var seeds = EjarListingAttributes.ForKind(kind);
            return Task.FromResult<AttributeTemplate?>(BuildFromSeeds(seeds));
        }

        // غَير مَعروف ⇒ universal (فارِغَة الآن — لا تَكرار مَع IListing).
        return Task.FromResult<AttributeTemplate?>(BuildFromSeeds(EjarListingAttributes.UniversalDefaults));
    }

    private static AttributeTemplate BuildFromSeeds(IReadOnlyList<AttributeSeed> seeds)
    {
        var sort = 0;
        var fields = seeds.Select(s => new AttributeFieldDefinition
        {
            Key      = s.Code,
            Label    = s.Name,
            LabelAr  = s.Name,
            Type     = MapType(s.Type),
            SortOrder = ++sort,
            Options  = (s.Options ?? Array.Empty<string>())
                        .Select(v => new AttributeOption
                        {
                            Value   = v,
                            Label   = v,
                            LabelAr = TranslateOption(s.Code, v),
                        })
                        .ToList(),
        }).ToList();
        return new AttributeTemplate { Fields = fields };
    }

    /// <summary>قاموس تَرجَمات لِخِيارات Ejar الافتِراضِيَّة.
    /// يَختار التَّرجَمَة المُحَدَّدَة لِلحَقل ثُمّ يَرجِع إلى قاموس
    /// <see cref="_genericOptions"/> العام (لِقِيَم تَتَكَرَّر عَبر حُقول
    /// كَ "yes/no", "indoor/outdoor", "monthly/yearly").</summary>
    private static string TranslateOption(string defCode, string value)
    {
        var specific = (defCode, value) switch
        {
            ("Floor", "ground")        => "أَرضي",
            ("Floor", "first")         => "الأَوَّل",
            ("Floor", "second")        => "الثاني",
            ("Floor", "third")         => "الثالِث",
            ("Floor", "fourth")        => "الرابِع",
            ("Floor", "fifth")         => "الخامِس",
            ("Floor", "sixth")         => "السادِس",
            ("Floor", "seventh")       => "السابِع",
            ("Floor", "eighth")        => "الثامِن",
            ("Floor", "ninth")         => "التاسِع",
            ("Floor", "tenth")         => "العاشِر",
            ("Furnished", "furnished")    => "مُؤَثَّث",
            ("Furnished", "unfurnished")  => "غَير مُؤَثَّث",
            ("Furnished", "semi")         => "نِصف مُؤَثَّث",
            ("Parking", "yes")            => "مُتَوَفِّر",
            ("Parking", "no")             => "غَير مُتَوَفِّر",
            ("Parking", "covered")        => "مُغَطّى",
            ("Parking", "street")         => "في الشارِع",
            _                             => null,
        };
        if (specific is not null) return specific;
        return _genericOptions.TryGetValue(value, out var v) ? v : value;
    }

    private static readonly Dictionary<string, string> _genericOptions = new()
    {
        // Yes/No
        ["yes"] = "نَعَم", ["no"] = "لا",

        // Lease term / payment schedules
        ["daily"] = "يَومي", ["weekly"] = "أُسبوعي", ["monthly"] = "شَهري",
        ["quarterly"] = "رُبع سَنَوي", ["semi_annual"] = "نِصف سَنَوي",
        ["annual"] = "سَنَوي", ["yearly"] = "سَنَوي", ["upfront"] = "دَفعَة واحِدَة",

        // Indoor/Outdoor
        ["indoor"] = "داخِلي", ["outdoor"] = "خارِجي", ["both"] = "كِلاهُما",

        // Utilities included
        ["all"] = "الكُلّ", ["electricity"] = "كَهرَباء",
        ["water"] = "ماء", ["internet"] = "إنتَرنِت", ["none"] = "لا شَيء",

        // Water source
        ["public"] = "شَبَكَة عامَّة", ["well"] = "بِئر", ["tanker"] = "وايِت",

        // Power backup
        ["generator"] = "مُولِّد", ["solar"] = "طاقَة شَمسِيَّة",
        ["battery"] = "بَطّارِيَّات", ["ups"] = "UPS",

        // View
        ["sea"] = "إطلالَة بَحرِيَّة", ["mountain"] = "إطلالَة جَبَلِيَّة",
        ["city"] = "إطلالَة عَلى المَدينَة", ["garden"] = "إطلالَة عَلى الحَديقَة",
        ["street"] = "إطلالَة عَلى الشارِع",

        // Orientation
        ["north"] = "شَمال", ["south"] = "جَنوب", ["east"] = "شَرق", ["west"] = "غَرب",
        ["northeast"] = "شَمال شَرقي", ["northwest"] = "شَمال غَربي",
        ["southeast"] = "جَنوب شَرقي", ["southwest"] = "جَنوب غَربي",

        // Condition
        ["new"] = "جَديد", ["excellent"] = "مُمتاز",
        ["good"] = "جَيِّد", ["fair"] = "مَقبول",
        ["needs_renovation"] = "يَحتاج صِيانَة",

        // Building type
        ["tower"] = "بُرج", ["house"] = "بَيت مُستَقِلّ", ["compound"] = "مُجَمَّع",
        ["commercial_building"] = "مَبنى تِجاري", ["mall"] = "مول",

        // Policies
        ["allowed"] = "مَسموح", ["not_allowed"] = "غَير مَسموح",
        ["case_by_case"] = "حَسب الحالَة", ["outdoor_only"] = "في الخارِج فَقَط",

        // Vehicle bodies
        ["sedan"] = "سيدان", ["suv"] = "SUV", ["hatchback"] = "هاتشباك",
        ["coupe"] = "كوبيه", ["pickup"] = "بِك أَب", ["van"] = "فان",
        ["bus"] = "باص", ["minibus"] = "باص صَغير",
        ["motorcycle"] = "درّاجَة ناريَّة", ["scooter"] = "سكوتَر",

        // Fuel
        ["petrol"] = "بَنزين", ["diesel"] = "ديزِل",
        ["electric"] = "كَهرَبائي", ["hybrid"] = "هايبرِد", ["gas"] = "غاز",

        // Transmission
        ["automatic"] = "أوتوماتيك", ["manual"] = "عادي",
        ["cvt"] = "CVT", ["dual_clutch"] = "دَبَل كلاتش",

        // Drive
        ["fwd"] = "دَفع أَمامي", ["rwd"] = "دَفع خَلفي",
        ["awd"] = "دَفع رُباعي دائِم", ["4wd"] = "دَفع رُباعي",

        // License
        ["light"] = "خَفيفَة", ["heavy"] = "ثَقيلَة",
        ["public_transport"] = "نَقل عامّ",

        // Event types
        ["wedding"] = "زَواج", ["engagement"] = "خِطبَة",
        ["graduation"] = "تَخَرُّج", ["birthday"] = "عيد ميلاد",
        ["corporate"] = "شَرِكات", ["conference"] = "مُؤتَمَر",
        ["funeral"] = "عَزاء", ["other"] = "أُخرى",

        // Slots
        ["morning"] = "صَباحي", ["evening"] = "مَسائي",

        // Cancel policy
        ["flexible"] = "مَرِنَة", ["moderate"] = "مُتَوَسِّطَة",
        ["strict"] = "صارِمَة", ["non_refundable"] = "غَير قابِلَة لِلاستِرداد",

        // Camp style / type
        ["modern"] = "حَديث", ["traditional"] = "تُراثي",
        ["desert"] = "صَحراوي", ["beach"] = "ساحِلي",
        ["weddings"] = "أَفراح", ["family"] = "عائِلي",
        ["tourism"] = "سِياحَة", ["mixed"] = "مُتَعَدِّد",
    };

    private static string MapType(string t) => t switch
    {
        "SingleSelect"             => "select",
        "MultiSelect"              => "multi",
        "Number"                   => "number",
        "Boolean"                  => "bool",
        "Date" or "DateTime"       => "date",
        _                          => "text",
    };
}
