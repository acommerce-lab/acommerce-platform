namespace ACommerce.OperationEngine.Core;

/// <summary>
/// أحداث دورة حياة العملية.
/// كل مرحلة لها Before/After يمكن ربط أي وظيفة بها.
/// </summary>
public class OperationLifecycleHooks
{
    public Func<OperationContext, Task>? BeforeAnalyze { get; set; }
    public Func<OperationContext, Task>? AfterAnalyze { get; set; }
    public Func<OperationContext, Task>? BeforeValidate { get; set; }
    public Func<OperationContext, Task>? AfterValidate { get; set; }
    public Func<OperationContext, Task>? BeforeExecute { get; set; }
    public Func<OperationContext, Task>? AfterExecute { get; set; }
    public Func<OperationContext, Task>? BeforeSubOperations { get; set; }
    public Func<OperationContext, Task>? AfterSubOperations { get; set; }
    public Func<OperationContext, Task>? BeforePostAnalyze { get; set; }
    public Func<OperationContext, Task>? AfterPostAnalyze { get; set; }
    public Func<OperationContext, Task>? BeforeComplete { get; set; }
    public Func<OperationContext, Task>? AfterComplete { get; set; }
    public Func<OperationContext, Task>? BeforeFail { get; set; }
    public Func<OperationContext, Task>? AfterFail { get; set; }
    public Func<OperationContext, Exception, Task>? BeforeError { get; set; }
    public Func<OperationContext, Exception, Task>? AfterError { get; set; }

    internal async Task InvokeAsync(Func<OperationContext, Task>? hook, OperationContext ctx)
    {
        if (hook != null) await hook(ctx);
    }

    internal async Task InvokeAsync(Func<OperationContext, Exception, Task>? hook, OperationContext ctx, Exception ex)
    {
        if (hook != null) await hook(ctx, ex);
    }
}
