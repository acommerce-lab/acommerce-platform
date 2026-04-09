using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Interceptors;
using Microsoft.Extensions.Logging;

namespace ACommerce.Permissions.Operations;

/// <summary>
/// تجريد فحص الصلاحيات - يُطبّقه التطبيق ليربط بنظام الأدوار/الصلاحيات الخاص به.
/// </summary>
public interface IPermissionResolver
{
    /// <summary>هل المستخدم يملك الإذن المطلوب؟</summary>
    Task<bool> HasPermissionAsync(Guid userId, string permission, CancellationToken ct = default);

    /// <summary>هل المستخدم في الدور المطلوب؟</summary>
    Task<bool> IsInRoleAsync(Guid userId, string role, CancellationToken ct = default);
}

/// <summary>
/// معترض فحص الصلاحيات - يعمل على أي قيد عليه علامة "permission_check" أو "role_check".
///
/// مفاتيح العلامات في القيد:
///   - "permission_check" → اسم الإذن (مثل: "listings.delete")
///   - "role_check" → اسم الدور (مثل: "admin")
///   - "permission_user_id" → معرّف المستخدم
/// </summary>
public class PermissionInterceptor : IOperationInterceptor
{
    private readonly IPermissionResolver _resolver;
    private readonly ILogger<PermissionInterceptor> _logger;

    public string Name => "PermissionInterceptor";
    public InterceptorPhase Phase => InterceptorPhase.Pre;

    public PermissionInterceptor(IPermissionResolver resolver, ILogger<PermissionInterceptor> logger)
    {
        _resolver = resolver;
        _logger = logger;
    }

    public bool AppliesTo(Operation op) =>
        op.HasTag(PermissionTagKeys.Check.Name) || op.HasTag(PermissionTagKeys.RoleCheck.Name);

    public async Task<AnalyzerResult> InterceptAsync(OperationContext context, OperationResult? result = null)
    {
        var op = context.Operation;
        var userId = ResolveUserId(op);
        if (userId == Guid.Empty)
            return AnalyzerResult.Fail("permission_user_not_resolved");

        var permission = op.GetTagValue(PermissionTagKeys.Check.Name);
        if (!string.IsNullOrEmpty(permission))
        {
            var has = await _resolver.HasPermissionAsync(userId, permission, context.CancellationToken);
            if (!has) return AnalyzerResult.Fail($"missing_permission:{permission}");
        }

        var role = op.GetTagValue(PermissionTagKeys.RoleCheck.Name);
        if (!string.IsNullOrEmpty(role))
        {
            var inRole = await _resolver.IsInRoleAsync(userId, role, context.CancellationToken);
            if (!inRole) return AnalyzerResult.Fail($"missing_role:{role}");
        }

        return AnalyzerResult.Pass();
    }

    private static Guid ResolveUserId(Operation op)
    {
        var explicitId = op.GetTagValue(PermissionTagKeys.UserId.Name);
        if (!string.IsNullOrEmpty(explicitId) && Guid.TryParse(explicitId, out var id))
            return id;

        // ابحث عن طرف بدور subject/owner
        foreach (var role in new[] { "subject", "owner", "actor", "subscriber" })
        {
            var party = op.GetPartiesByTag("role", role).FirstOrDefault();
            if (party == null) continue;

            var colonIdx = party.Identity.IndexOf(':');
            if (colonIdx > 0 && Guid.TryParse(party.Identity[(colonIdx + 1)..], out var partyId))
                return partyId;
        }

        return Guid.Empty;
    }
}
