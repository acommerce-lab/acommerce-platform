using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.Kits.Support.Backend;

public static class SupportKitPolicies
{
    /// <summary>أيّ مستخدم موثَّق يستطيع فتح/قراءة/الردّ على تذاكره.</summary>
    public const string User  = "Support.User";

    /// <summary>الوكلاء/الإدارة — تغيير الحالة، تخصيص أعضاء، إلخ.</summary>
    public const string Agent = "Support.Agent";

    public static IServiceCollection AddSupportKitPolicies(this IServiceCollection services)
    {
        services.AddAuthorization(opts =>
        {
            opts.AddPolicy(User,  p => p.RequireAuthenticatedUser());
            opts.AddPolicy(Agent, p => p.RequireAuthenticatedUser().RequireRole("admin", "Admin", "agent"));
        });
        return services;
    }
}
