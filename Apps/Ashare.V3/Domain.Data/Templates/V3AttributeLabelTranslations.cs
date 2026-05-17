namespace Ashare.V3.Data.Templates;

/// <summary>
/// خَريطَة تَرجَمات إنجِليزِيَّة لِتَسميات حُقول السِمات الديناميكِيَّة في V3.
/// الـ <c>AttributeDefinitions.Name</c> يُحفَظ عَرَبيّاً (لِبَيانات لوحَة
/// إدارَة عَرَبِيَّة افتِراضِيَّة). الـ widgets في الواجِهَة الأَمامِيَّة
/// تَحتاج تَسمِيَة إنجِليزِيَّة عِندَ لُغَة المُستَخدِم = en.
///
/// <para>المَنطِق: نُخَزِّن العَرَبي كَ <c>LabelAr</c>، وَ نَبحَث هُنا عَن
/// مُقابِله الإنجِليزي بِالـ <c>Code</c>. لَو لَم يوجَد، نَستَخدِم العَرَبي
/// كَ fallback. هذه القائِمَة قابِلَة لِلتَّوسيع — كُلّ كود سِمَة جَديد
/// يُضاف هُنا، أَو يَبقى يَعرِض العَرَبي.</para>
/// </summary>
public static class V3AttributeLabelTranslations
{
    public static string? TryEnglish(string code) =>
        _en.TryGetValue(code, out var en) ? en : null;

    private static readonly Dictionary<string, string> _en = new(StringComparer.Ordinal)
    {
        // ─── Profile sentinel ────────────────────────────────────────────
        ["Bio"]              = "Bio",
        ["Occupation"]       = "Occupation",
        ["Nationality"]      = "Nationality",
        ["Languages"]        = "Languages",
        ["BusinessName"]     = "Business name",
        ["Address"]          = "Address",
        ["Country"]          = "Country",
        ["PostalCode"]       = "Postal code",
        ["Coordinates"]      = "Coordinates",
        ["Type"]             = "Type",
        ["IsActive"]         = "Active",
        ["IsVerified"]       = "Verified",
        ["VerifiedAt"]       = "Verified at",

        // ─── Roommate (Has a room) ──────────────────────────────────────
        ["RoomPrice"]        = "Monthly room price (SAR)",
        ["BedroomShare"]     = "Room type",
        ["RoomCount"]        = "Bedrooms in the apartment",
        ["BathroomCount"]    = "Bathroom count",
        ["PrivateBathroom"]  = "Private bathroom",
        ["RoommatesPresent"] = "Current roommates",
        ["PropertyType"]     = "Property type",
        ["Floor"]            = "Floor",
        ["AreaSqm"]          = "Area (m²)",
        ["AvailableFrom"]    = "Available from",
        ["MinimumStay"]      = "Minimum stay",
        ["DepositAmount"]    = "Deposit",
        ["UtilitiesIncluded"]= "Utilities included",
        ["Furnished"]        = "Furnished",
        ["Wifi"]             = "Internet",
        ["AirConditioning"]  = "Air conditioning",
        ["HotWater"]         = "Hot water",
        ["Kitchen"]          = "Shared kitchen",
        ["Laundry"]          = "Washing machine",
        ["Parking"]          = "Parking",
        ["Elevator"]         = "Elevator",
        ["PowerBackup"]      = "Backup power",
        ["WaterTank"]        = "Water tank",
        ["GenderPref"]       = "Preferred gender",
        ["MinAgePref"]       = "Minimum preferred age",
        ["MaxAgePref"]       = "Maximum preferred age",
        ["OccupationPref"]   = "Preferred roommate occupation",
        ["Smoking"]          = "Smoking",
        ["Pets"]             = "Pets",
        ["Cleanliness"]      = "Cleanliness level",
        ["Lifestyle"]        = "Lifestyle",
        ["VisitorsPolicy"]   = "Visitors policy",
        ["Religion"]         = "Religion",
        ["RoommateBio"]      = "General vibe description",

        // ─── Roommate (Looking for a room) ──────────────────────────────
        ["Age"]              = "Age",
        ["Gender"]           = "Gender",
        ["MaritalStatus"]    = "Marital status",
        ["AboutMe"]          = "About me",
        ["Budget"]           = "Monthly budget (SAR)",
        ["PreferredArea"]    = "Preferred area",
        ["PreferredCities"]  = "Preferred cities",
        ["PreferredPropertyType"] = "Preferred property type",
        ["FurnishedPref"]    = "Preferred furnishing",
        ["MoveInBy"]         = "Move-in date",
        ["StayDuration"]     = "Expected stay duration",
        ["Smoker"]           = "Smoker",
        ["HasPet"]           = "Owns a pet",
        ["RoommateGenderPref"]  = "Preferred roommate gender",
        ["RoommateMinAge"]      = "Roommate min age",
        ["RoommateMaxAge"]      = "Roommate max age",
        ["RoommateCountPref"]   = "Preferred roommate count",
    };
}
