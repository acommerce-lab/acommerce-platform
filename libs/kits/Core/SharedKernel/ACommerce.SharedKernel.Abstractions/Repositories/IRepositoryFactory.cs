using ACommerce.SharedKernel.Abstractions.Entities;

namespace ACommerce.SharedKernel.Abstractions.Repositories;

/// <summary>
/// مصنع المستودعات (Repository Factory Pattern)
/// </summary>
public interface IRepositoryFactory
{
	/// <summary>
	/// إنشاء مستودع لكيان معين
	/// </summary>
	IBaseAsyncRepository<T> CreateRepository<T>() where T : class, IBaseEntity;
}
