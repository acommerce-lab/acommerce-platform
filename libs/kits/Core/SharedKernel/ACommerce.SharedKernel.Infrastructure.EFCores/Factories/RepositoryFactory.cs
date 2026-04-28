using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ACommerce.SharedKernel.Domain.Entities;
using ACommerce.SharedKernel.Repositories.Interfaces;
using ACommerce.SharedKernel.Infrastructure.EFCore.Repositories;

namespace ACommerce.SharedKernel.Infrastructure.EFCore.Factories;

/// <summary>
/// „’‰⁄ «·„” Êœ⁄« 
/// </summary>
public class RepositoryFactory(IServiceProvider serviceProvider) : IRepositoryFactory
{
    private readonly IServiceProvider _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

    public IBaseAsyncRepository<T> CreateRepository<T>() where T : class, IBaseEntity
	{
		var dbContext = _serviceProvider.GetRequiredService<DbContext>();
		var logger = _serviceProvider.GetRequiredService<ILogger<BaseAsyncRepository<T>>>();

		return new BaseAsyncRepository<T>(dbContext, logger);
	}
}
