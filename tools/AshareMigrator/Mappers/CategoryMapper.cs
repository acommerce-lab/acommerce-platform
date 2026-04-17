using ACommerce.SharedKernel.Abstractions.DynamicAttributes;
using AshareMigrator.Legacy;
using AshareMigrator.Target;
using AshareMigrator.Templates;

namespace AshareMigrator.Mappers;

public static class CategoryMapper
{
    public static NewCategory Map(LegacyCategory src)
    {
        var slug = NormalizeSlug(src.Slug);
        var template = ResolveTemplate(slug);

        return new NewCategory
        {
            Id = src.Id,
            CreatedAt = DateTime.SpecifyKind(src.CreatedAt, DateTimeKind.Utc),
            UpdatedAt = src.UpdatedAt,
            IsDeleted = false,
            Slug = slug,
            NameAr = src.Name,
            NameEn = PickEnglishName(slug),
            Description = src.Description,
            Icon = src.Icon,
            SortOrder = src.SortOrder,
            IsActive = src.IsActive,
            AttributeTemplateJson = template != null
                ? DynamicAttributeHelper.SerializeTemplate(template)
                : null,
        };
    }

    public static AttributeTemplate? ResolveTemplate(string slug) => slug switch
    {
        "residential" => AshareTemplates.Residential(),
        "looking-for-housing" or "looking_for_housing" => AshareTemplates.LookingForHousing(),
        "looking-for-partner" or "looking_for_partner" => AshareTemplates.LookingForPartner(),
        "administrative" => AshareTemplates.Administrative(),
        "commercial" => AshareTemplates.Commercial(),
        _ => null
    };

    private static string NormalizeSlug(string slug) => slug.Replace('_', '-').ToLowerInvariant();

    private static string PickEnglishName(string slug) => slug switch
    {
        "residential" => "Residential",
        "looking-for-housing" => "Looking for Housing",
        "looking-for-partner" => "Looking for Partner",
        "administrative" => "Administrative",
        "commercial" => "Commercial",
        _ => slug
    };
}
