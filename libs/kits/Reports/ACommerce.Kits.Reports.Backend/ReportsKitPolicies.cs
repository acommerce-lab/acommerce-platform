using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.Kits.Reports.Backend;

public static class ReportsKitPolicies
{
    /// <summary>أيّ مستخدم موثَّق يستطيع تقديم بلاغ + قراءة بلاغاته.</summary>
    public const string Reporter  = "Reports.Reporter";

    /// <summary>الإدارة/الوكلاء — قراءة كلّ البلاغات + تغيير الحالة.</summary>
    public const string Moderator = "Reports.Moderator";

    public static IServiceCollection AddReportsKitPolicies(this IServiceCollection services)
    {
        services.AddAuthorization(opts =>
        {
            opts.AddPolicy(Reporter,  p => p.RequireAuthenticatedUser());
            opts.AddPolicy(Moderator, p => p.RequireAuthenticatedUser().RequireRole("admin", "Admin", "agent"));
        });
        return services;
    }
}
