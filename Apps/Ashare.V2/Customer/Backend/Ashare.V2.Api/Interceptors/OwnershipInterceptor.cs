using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Interceptors;

namespace Ashare.V2.Api.Interceptors;

/// <summary>
/// Cross-cutting ownership policy for mutating operations that touch a resource
/// owned by some user.
///
/// يُطلَق على أيّ عمليّة تحمل الـ tag <c>owner_policy</c> مع قيمة إحدى:
///   - "must_own"     → يجب أن يكون المتصرّف هو المالك (مثال: toggle listing).
///   - "must_not_own" → يجب ألّا يكون المتصرّف هو المالك (مثال: book, message).
///
/// يحتاج العمليّة تُضيف:
///   .From($"User:{callerId}", 1, ("role","…"))
///   .Tag("owner_policy", "must_own" | "must_not_own")
///   .Tag("resource_owner", ownerId)
/// </summary>
public sealed class OwnershipInterceptor : IOperationInterceptor
{
    public string Name => "ashare.ownership";
    public InterceptorPhase Phase => InterceptorPhase.Pre;

    public bool AppliesTo(Operation op) => op.HasTag("owner_policy");

    public Task<AnalyzerResult> InterceptAsync(OperationContext context, OperationResult? _ = null)
    {
        var op = context.Operation;
        var policy = op.GetTagValue("owner_policy");
        var owner  = op.GetTagValue("resource_owner");
        if (string.IsNullOrEmpty(policy) || string.IsNullOrEmpty(owner))
            return Task.FromResult(AnalyzerResult.Fail("ownership_misconfigured"));

        // الطرف الأوّل (From) هو المتصرّف — صيغة Identity: "User:{id}".
        var caller = op.Parties.FirstOrDefault()?.Identity ?? string.Empty;
        var callerId = caller.Contains(':') ? caller.Split(':', 2)[1] : caller;
        var isOwner = string.Equals(callerId, owner, StringComparison.Ordinal);

        return policy switch
        {
            "must_own"     when !isOwner => Task.FromResult(AnalyzerResult.Fail("not_owner")),
            "must_not_own" when  isOwner => Task.FromResult(AnalyzerResult.Fail("self_action")),
            _ => Task.FromResult(AnalyzerResult.Pass())
        };
    }
}
