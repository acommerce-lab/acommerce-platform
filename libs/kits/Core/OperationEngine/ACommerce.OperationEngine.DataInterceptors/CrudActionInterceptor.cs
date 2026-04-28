using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Interceptors;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.OperationEngine.DataInterceptors;

/// <summary>
/// معترض عام يلتقط أي عملية تحتوي على Tag <c>db_action</c>.
/// يُسجَّل كـ Singleton في OperationInterceptorRegistry ويحل ISender
/// من OperationContext.Services (Scoped per request).
/// </summary>
public class CrudActionInterceptor : IOperationInterceptor
{
    public string Name => "CrudDataInterceptor";
    public InterceptorPhase Phase => InterceptorPhase.Post;

    public bool AppliesTo(Operation op) => op.HasTag(OperationTags.DbAction);

    public async Task<AnalyzerResult> InterceptAsync(OperationContext context, OperationResult? result = null)
    {
        var action = context.Operation.GetTagValue(OperationTags.DbAction);
        var target = context.Operation.GetTagValue(OperationTags.TargetEntity);

        if (string.IsNullOrEmpty(action) || string.IsNullOrEmpty(target))
            return AnalyzerResult.Pass();

        var mediator = context.Services.GetService<ISender>();
        if (mediator is null)
            return AnalyzerResult.Pass();

        try
        {
            var command = new DataOperationCommand(context.Operation, action, target);
            var success = await mediator.Send(command, context.CancellationToken);

            if (!success)
                return AnalyzerResult.Fail($"فشلت العملية البيانية: {action} على {target}", blocking: false);

            return AnalyzerResult.Pass();
        }
        catch (Exception ex)
        {
            return AnalyzerResult.Fail(ex.Message, blocking: false);
        }
    }
}
