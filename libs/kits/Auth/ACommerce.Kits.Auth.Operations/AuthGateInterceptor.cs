using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Interceptors;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ACommerce.Kits.Auth.Operations;

/// <summary>
/// معترض المصادقة الكونيّ — Pre، يتحقّق أنّ المستدعي مصادَق عليه قبل تنفيذ
/// أيّ عمليّة تحمل تاج <see cref="AuthTagKeys.RequiresAuth"/>.
///
/// <para>التصميم متوازٍ تماماً مع <c>VersionGateInterceptor</c>:
///   - يعمل في طبقة OpEngine لا في middleware ASP.NET (يعمل حتى للعمليّات
///     المُستدعاة محليّاً من جوب أو dispatcher).
///   - يتجاوز عمليّات <c>auth.*</c> الذاتيّة (مثلاً <c>auth.otp.verify</c>)
///     لأنّها تنشئ الجلسة أصلاً.
///   - يعتمد على <c>HttpContext.User</c> الذي يضعه middleware المصادقة في ASP.NET.
/// </para>
///
/// <para>التطبيق يفعّل المعترض على عمليّاته بإضافة التاج صراحةً:
/// <code>
///   Entry.Create("listing.create")
///       .Tag(AuthTagKeys.RequiresAuth, "true")
///       ...
/// </code>
/// أو يطبّقه عالميّاً على كل العمليّات بقالب مساعد.
/// التاج الصريح يجعل الانتقال تدريجياً وآمناً.</para>
/// </summary>
public sealed class AuthGateInterceptor : IOperationInterceptor
{
    public string Name => nameof(AuthGateInterceptor);
    public InterceptorPhase Phase => InterceptorPhase.Pre;

    public bool AppliesTo(Operation op)
    {
        if (op.HasTag(AuthTagKeys.SkipAuthGate)) return false;
        if (op.Type.StartsWith("auth.", StringComparison.Ordinal)) return false;
        return op.HasTag(AuthTagKeys.RequiresAuth);
    }

    public Task<AnalyzerResult> InterceptAsync(OperationContext context, OperationResult? result = null)
    {
        var http = context.Services.GetService<IHttpContextAccessor>()?.HttpContext;
        var isAuthenticated = http?.User?.Identity?.IsAuthenticated == true;

        if (isAuthenticated)
            return Task.FromResult(AnalyzerResult.Pass());

        var logger = context.Services.GetService<ILogger<AuthGateInterceptor>>();
        logger?.LogInformation(
            "Auth gate rejected operation {OpType} — caller not authenticated", context.Operation.Type);

        return Task.FromResult(new AnalyzerResult
        {
            Passed  = false,
            Message = AuthTagKeys.RejectionCode_NotAuthenticated,
            Data    = new Dictionary<string, object>
            {
                ["operation"] = context.Operation.Type
            }
        });
    }
}
