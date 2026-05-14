namespace Ashare.V3.Data.Templates;

/// <summary>
/// قاموس تَرجَمَة مَركَزي لِقِيَم خِيارات السِمات المَعروفَة في
/// asharedb. الإنتاج يَحفَظ <c>AttributeValues.DisplayName</c> بِالإنجليزِيَّة
/// (<c>"first"</c>, <c>"yes"</c>, <c>"male"</c>...)، وَ
/// <c>ProductionAttributeTemplateSource.MapOptions</c> يُغَذّي <c>Label</c>
/// وَ<c>LabelAr</c> مَن نَفس الحَقل ⇒ المُستَخدِم يَرى إنجليزي حَتى في
/// الواجِهَة العَرَبِيَّة.
///
/// <para>الحَلّ هُنا: <see cref="TryTranslate"/> يَأخُذ <c>(defCode, value)</c>
/// ويُعيد التَرجَمَة العَرَبِيَّة لَو مَوجودَة. <c>MapOptions</c> يَستَدعي
/// هذا قَبل بِناء <c>LabelAr</c>. القاموس مَكتوب مَرَّة واحِدَة + مَطبوع
/// (lowercase + بِلا underscore) لِيَلتَقِط الـ aliases.</para>
///
/// <para><b>التَوسيع</b>: لِإضافَة قِيَم جَديدَة، أَضِف entry في
/// <see cref="_byDef"/>. الـ Code key يَتَبع نَفس normalization
/// الـ <see cref="ProductionAttributeTemplateSource.Normalize"/>
/// (<c>"property_type"</c> ⇒ <c>"propertytype"</c>).</para>
/// </summary>
public static class V3AttributeValueTranslations
{
    /// <summary>يُجَرِّب التَرجَمَة لِأَيّ مَن القِيَم المُمَرَّرَة بِالتَّرتيب
    /// (Value أَوَّلاً ثُمّ DisplayName). يَعود null إن لَم يَتَطابَق شَيء.</summary>
    public static string? TryTranslate(string defCode, params string?[] values)
    {
        var nDef = Normalize(defCode);
        if (!_byDef.TryGetValue(nDef, out var defMap))
        {
            // لا قاموس مُخَصَّص لِهذا الـ def — جَرِّب القاموس العامّ.
            foreach (var v in values)
            {
                if (string.IsNullOrWhiteSpace(v)) continue;
                if (_common.TryGetValue(Normalize(v), out var commonAr)) return commonAr;
            }
            return null;
        }
        foreach (var v in values)
        {
            if (string.IsNullOrWhiteSpace(v)) continue;
            var nVal = Normalize(v);
            if (defMap.TryGetValue(nVal, out var ar)) return ar;
            if (_common.TryGetValue(nVal, out var commonAr)) return commonAr;
        }
        return null;
    }

    private static string Normalize(string s)
    {
        Span<char> buf = stackalloc char[s.Length];
        int n = 0;
        foreach (var c in s)
            if (char.IsLetterOrDigit(c)) buf[n++] = char.ToLowerInvariant(c);
        return new string(buf[..n]);
    }

    // قِيَم عامَّة تَظهَر في عِدَّة sims (boolean-like).
    private static readonly Dictionary<string, string> _common = new(StringComparer.Ordinal)
    {
        ["yes"] = "نَعَم",      ["no"] = "لا",
        ["true"] = "نَعَم",     ["false"] = "لا",
        ["1"] = "نَعَم",        ["0"] = "لا",
        ["available"] = "مُتَوَفِّر",
        ["notavailable"] = "غَير مُتَوَفِّر",
        ["any"] = "أَيّ",
        ["other"] = "أُخرى",
        ["none"] = "لا شَيء",
    };

    private static readonly Dictionary<string, Dictionary<string, string>> _byDef = new(StringComparer.Ordinal)
    {
        // الطَوابِق
        ["floor"] = new(StringComparer.Ordinal)
        {
            ["basement"] = "بَدروم",
            ["ground"]   = "أَرضي",
            ["first"]    = "الأَوَّل",
            ["second"]   = "الثاني",
            ["third"]    = "الثالِث",
            ["fourth"]   = "الرابِع",
            ["fifth"]    = "الخامِس",
            ["sixth"]    = "السادِس",
            ["seventh"]  = "السابِع",
            ["eighth"]   = "الثامِن",
            ["ninth"]    = "التاسِع",
            ["tenth"]    = "العاشِر",
            ["roof"]     = "السَطح",
            ["upper"]    = "عُلوي",
            ["mezzanine"] = "ميزانين",
        },

        // التَأثيث
        ["furnished"] = new(StringComparer.Ordinal)
        {
            ["furnished"]     = "مُؤَثَّث",
            ["unfurnished"]   = "غَير مُؤَثَّث",
            ["semi"]          = "نِصف مُؤَثَّث",
            ["semifurnished"] = "نِصف مُؤَثَّث",
        },

        // الجِنس
        ["gender"] = new(StringComparer.Ordinal)
        {
            ["male"]   = "ذَكَر",
            ["female"] = "أُنثى",
            ["both"]   = "كِلاهُما",
            ["family"] = "عائِلَة",
            ["single"] = "أَعزَب",
        },

        // وَحدَة الوَقت
        ["timeunit"] = new(StringComparer.Ordinal)
        {
            ["hourly"]  = "ساعَة",
            ["daily"]   = "يَومي",
            ["weekly"]  = "أُسبوعي",
            ["monthly"] = "شَهري",
            ["yearly"]  = "سَنَوي",
            ["annual"]  = "سَنَوي",
            ["once"]    = "لِمَرَّة واحِدَة",
        },

        // نَوع الإيجار/الإعلان
        ["rentaltype"] = new(StringComparer.Ordinal)
        {
            ["short"] = "قَصير",
            ["long"]  = "طَويل",
            ["daily"] = "يَومي",
            ["monthly"] = "شَهري",
            ["yearly"] = "سَنَوي",
        },
        ["billtype"] = new(StringComparer.Ordinal)
        {
            ["offer"]   = "عَرض",
            ["request"] = "طَلَب",
            ["wanted"]  = "مَطلوب",
        },

        // نوع الوَحدَة
        ["unittype"] = new(StringComparer.Ordinal)
        {
            ["apartment"]  = "شَقَّة",
            ["villa"]      = "فيلا",
            ["studio"]     = "اِستوديو",
            ["room"]       = "غُرفَة",
            ["house"]      = "بَيت",
            ["floor"]      = "دَور",
            ["building"]   = "عِمارَة",
            ["land"]       = "أَرض",
            ["shop"]       = "مَحَلّ",
            ["office"]     = "مَكتَب",
            ["warehouse"]  = "مُستَودَع",
            ["compound"]   = "مُجَمَّع",
            ["chalet"]     = "شاليه",
            ["farm"]       = "مَزرَعَة",
        },

        // نَوع العَقار
        ["propertytype"] = new(StringComparer.Ordinal)
        {
            ["residential"] = "سَكَني",
            ["commercial"]  = "تِجاري",
            ["industrial"]  = "صِناعي",
            ["agricultural"] = "زِراعي",
            ["mixed"]       = "مُختَلَط",
        },

        // عَدَد الغُرَف/دَوَرات المياه
        ["rooms"] = new(StringComparer.Ordinal)
        {
            ["studio"] = "اِستوديو",
            ["1"] = "١", ["2"] = "٢", ["3"] = "٣", ["4"] = "٤",
            ["5"] = "٥", ["6"] = "٦", ["7"] = "٧",
            ["8plus"] = "٨ أَو أَكثَر",
        },
        ["bathrooms"] = new(StringComparer.Ordinal)
        {
            ["1"] = "١", ["2"] = "٢", ["3"] = "٣", ["4"] = "٤",
            ["5"] = "٥", ["6plus"] = "٦ أَو أَكثَر",
        },

        // المَواقِف
        ["parking"] = new(StringComparer.Ordinal)
        {
            ["yes"]      = "مُتَوَفِّر",
            ["no"]       = "غَير مُتَوَفِّر",
            ["covered"]  = "مُغَطّى",
            ["open"]     = "مَفتوح",
            ["multiple"] = "مُتَعَدِّد",
        },

        // التَدخين
        ["smoking"] = new(StringComparer.Ordinal)
        {
            ["allowed"]    = "مَسموح",
            ["notallowed"] = "غَير مَسموح",
            ["outdoor"]    = "خارِجي فَقَط",
        },

        // الجِنسِيَّة
        ["nationality"] = new(StringComparer.Ordinal)
        {
            ["saudi"]     = "سُعودي",
            ["yemeni"]    = "يَمَني",
            ["egyptian"]  = "مَصري",
            ["arab"]      = "عَرَبي",
            ["foreigner"] = "أَجنَبي",
            ["any"]       = "أَيّ",
        },
    };
}
