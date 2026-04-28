using ACommerce.SharedKernel.Domain.Entities;

namespace ACommerce.SharedKernel.Repositories.Interfaces;

/// <summary>
/// ãÕäÚ ÇáãÓÊæÏÚÇÊ (Repository Factory Pattern)
/// </summary>
public interface IRepositoryFactory
{
	/// <summary>
	/// ÅäÔÇÁ ãÓÊæÏÚ áßíÇä ãÚíä
	/// </summary>
	IBaseAsyncRepository<T> CreateRepository<T>() where T : class, IBaseEntity;
}
