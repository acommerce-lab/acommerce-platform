using ACommerce.SharedKernel.Domain.Entities;

namespace ACommerce.SharedKernel.Mediators;

/// <summary>
/// وسيط لعمليات البيانات (Mediator/Interceptors Layer)
/// يسمح بفصل طلب العملية عن تنفيذها الفعلي.
/// </summary>
public interface IDataOperationMediator
{
    /// <summary>
    /// إرسال طلب تنفيذ عملية بيانات (Create, Update, Delete)
    /// </summary>
    Task<bool> SendAsync<TEntity>(DataOperationRequest<TEntity> request, CancellationToken ct = default) 
        where TEntity : class, IBaseEntity;
}

public enum DataAction
{
    Create,
    Update,
    Delete,
    SoftDelete,
    Restore
}

public record DataOperationRequest<TEntity>(
    TEntity Entity, 
    DataAction Action, 
    string? Reason = null, 
    Dictionary<string, object>? Metadata = null
) where TEntity : class, IBaseEntity;
