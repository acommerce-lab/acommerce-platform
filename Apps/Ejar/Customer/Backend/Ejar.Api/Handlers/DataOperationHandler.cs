using ACommerce.OperationEngine.DataInterceptors;
using ACommerce.SharedKernel.Domain.Entities;
using ACommerce.SharedKernel.Repositories.Interfaces;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Reflection;
using ACommerce.SharedKernel.Domain.Entities;

namespace Ejar.Api.Handlers;

/// <summary>
/// المعالج العام لعمليات البيانات (Generic CRUD Handler).
/// يدعم القيم النصية القديمة والقيم الجديدة من نوع OperationType.
/// </summary>
public class DataOperationHandler(IRepositoryFactory repositoryFactory, ILogger<DataOperationHandler> logger) 
    : IRequestHandler<DataOperationCommand, bool>
{
    public async Task<bool> Handle(DataOperationCommand request, CancellationToken ct)
    {
        var ctx = request.Context;
        var action = request.DbAction;
        var targetName = request.TargetEntity;

        // 1. العثور على نوع الكيان من السجل
        var entityType = EntityDiscoveryRegistry.GetRegisteredTypes()
            .FirstOrDefault(t => t.Name.Equals(targetName, StringComparison.OrdinalIgnoreCase) 
                              || t.Name.Equals(targetName + "Entity", StringComparison.OrdinalIgnoreCase));

        if (entityType == null)
        {
            logger.LogError("Entity type '{Target}' not found in EntityDiscoveryRegistry.", targetName);
            return false;
        }

        // 2. إنشاء المستودع ديناميكياً
        var method = repositoryFactory.GetType().GetMethod(nameof(IRepositoryFactory.CreateRepository))!
            .MakeGenericMethod(entityType);
        
        var repository = method.Invoke(repositoryFactory, null);
        if (repository == null) return false;

        // 3. تنفيذ الإجراء المطلوب (دعم القيم الجديدة والقديمة)
        try
        {
            return action.ToLower() switch
            {
                // القيم الجديدة (DataOperationTypes)
                "data.create"      => await HandleCreate(ctx, entityType, repository, ct),
                "data.read_all"    => await HandleRead(ctx, entityType, repository, ct),
                "data.read_by_id"  => await HandleRead(ctx, entityType, repository, ct),
                "data.update"      => await HandleUpdate(ctx, entityType, repository, ct),
                "data.delete"      => await HandleDelete(ctx, entityType, repository, ct),

                // القيم القديمة (للتوافق)
                OperationTags.DbActions.Create => await HandleCreate(ctx, entityType, repository, ct),
                OperationTags.DbActions.Read   => await HandleRead(ctx, entityType, repository, ct),
                OperationTags.DbActions.Update => await HandleUpdate(ctx, entityType, repository, ct),
                OperationTags.DbActions.Delete => await HandleDelete(ctx, entityType, repository, ct),
                
                _ => false
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error executing generic DB action {Action} on {Target}", action, targetName);
            ctx.AddValidationError($"Database error: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> HandleRead(ACommerce.OperationEngine.Core.OperationContext ctx, Type entityType, object repository, CancellationToken ct)
    {
        // إذا وجدنا Id في Metadata، نقوم بجلب سجل واحد
        if (ctx.Operation.Metadata.TryGetValue("id", out var idObj) && idObj is Guid id)
        {
            var getMethod = repository.GetType().GetMethod("GetByIdAsync", [typeof(Guid), typeof(CancellationToken)]);
            if (getMethod == null) return false;

            var task = (Task)getMethod.Invoke(repository, [id, ct])!;
            await task;
            
            var result = task.GetType().GetProperty("Result")?.GetValue(task);
            ctx.Set("db_result", result);
            return true;
        }

        // وإلا نجلب القائمة كاملة
        var listMethod = repository.GetType().GetMethod("ListAllAsync", [typeof(CancellationToken)]);
        if (listMethod == null) return false;

        var listTask = (Task)listMethod.Invoke(repository, [ct])!;
        await listTask;
        
        var listResult = listTask.GetType().GetProperty("Result")?.GetValue(listTask);
        ctx.Set("db_result", listResult);
        return true;
    }

    private async Task<bool> HandleCreate(ACommerce.OperationEngine.Core.OperationContext ctx, Type entityType, object repository, CancellationToken ct)
    {
        var entity = ResolveEntity(ctx, entityType);
        if (entity is null) return false;

        var addMethod = repository.GetType().GetMethod("AddAsync", [entityType, typeof(CancellationToken)]);
        if (addMethod == null) return false;

        var task = (Task)addMethod.Invoke(repository, [entity, ct])!;
        await task;

        var result = task.GetType().GetProperty("Result")?.GetValue(task);
        ctx.Set("db_result", result);
        return true;
    }

    private async Task<bool> HandleUpdate(ACommerce.OperationEngine.Core.OperationContext ctx, Type entityType, object repository, CancellationToken ct)
    {
        var entity = ResolveEntity(ctx, entityType);
        if (entity is null) return false;

        var updateMethod = repository.GetType().GetMethod("UpdateAsync", [entityType, typeof(CancellationToken)]);
        if (updateMethod == null) return false;

        var task = (Task)updateMethod.Invoke(repository, [entity, ct])!;
        await task;
        return true;
    }

    private async Task<bool> HandleDelete(ACommerce.OperationEngine.Core.OperationContext ctx, Type entityType, object repository, CancellationToken ct)
    {
        var entity = ResolveEntity(ctx, entityType);
        if (entity is null) return false;

        var deleteMethod = repository.GetType().GetMethod("DeleteAsync", [entityType, typeof(CancellationToken)]);
        if (deleteMethod == null) return false;

        var task = (Task)deleteMethod.Invoke(repository, [entity, ct])!;
        await task;
        return true;
    }

    /// <summary>
    /// يبحث عن الـ entity في عدّة مواضع، بترتيب الأولويّة:
    ///   ١. <c>ctx.Entity&lt;TEntity&gt;()</c> (F1 — typed، مفضَّل).
    ///   ٢. <c>op.Metadata["entity"]</c> (المسار القديم، يبقى للتوافق).
    /// إن لم يُعثَر، يُرجع null والمعالج يُهمل العمليّة.
    /// </summary>
    private static object? ResolveEntity(ACommerce.OperationEngine.Core.OperationContext ctx, Type entityType)
    {
        // ① مسار F1 الجديد: ctx.Entity<TEntity>() — يبحث في items عبر key
        // "_entity:" + typeName. يدعم كلّ من concrete entity أو واجهة.
        var key = "_entity:" + entityType.FullName;
        if (ctx.Items.TryGetValue(key, out var byType) && byType is not null
            && entityType.IsInstanceOfType(byType))
            return byType;

        // ②  مسار قديم: op.Metadata["entity"] — يقبل أيّ object يُطابق نوعاً.
        if (ctx.Operation.Metadata.TryGetValue("entity", out var legacy) && legacy is not null
            && entityType.IsInstanceOfType(legacy))
            return legacy;

        return null;
    }
}
