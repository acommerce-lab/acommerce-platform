using Ejar.Customer.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace Ejar.Customer.UI;

/// <summary>
/// Backward-compat wrapper. كلّ الخَدَمات المُشتَرَكة انتقلت إلى
/// <see cref="EjarCustomerSharedExtensions.AddEjarCustomerShared"/> في
/// <c>Ejar.Customer.Shared</c>. هذه الدالّة تَستدعيها فقط — حافظنا على
/// الاسم لتَجَنّب كَسر V1's Program.cs.
/// </summary>
public static class EjarCustomerUiExtensions
{
    public static IServiceCollection AddEjarCustomerUI(this IServiceCollection services)
        => services.AddEjarCustomerShared();
}
