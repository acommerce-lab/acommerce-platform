using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ACommerce.SharedKernel.Infrastructure.EFCores.Context;
using ACommerce.SharedKernel.Abstractions.Repositories;
using ACommerce.SharedKernel.Infrastructure.EFCore.Factories;

namespace ACommerce.SharedKernel.Infrastructure.EFCores.Extensions;

/// <summary>
/// Extension methods لتسجيل ACommerce Database Context
/// </summary>
public static class ServiceCollectionExtensions
{
	/// <summary>
	/// تسجيل ApplicationDbContext مع Auto-Discovery
	/// سطر واحد يكفي! ✨
	/// </summary>
	public static IServiceCollection AddACommerceDbContext(
		this IServiceCollection services,
		Action<DbContextOptionsBuilder> optionsAction)
	{
		// تسجيل ApplicationDbContext
		services.AddDbContext<ApplicationDbContext>(optionsAction);

		// تسجيل DbContext العادي (للتوافق مع الكود القديم)
		services.AddScoped<DbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());

		// تسجيل Repository Factory
		services.AddScoped<IRepositoryFactory, RepositoryFactory>();

		return services;
	}

	/// <summary>
	/// تسجيل InMemory Database (للتجربة)
	/// </summary>
	public static IServiceCollection AddACommerceInMemoryDatabase(
		this IServiceCollection services,
		string databaseName = "ACommerceDb")
	{
		return services.AddACommerceDbContext(options =>
			options.UseInMemoryDatabase(databaseName));
	}

	/// <summary>
	/// تسجيل SQL Server
	/// </summary>
	public static IServiceCollection AddACommerceSqlServer(
		this IServiceCollection services,
		string connectionString)
	{
		return services.AddACommerceDbContext(options =>
			options.UseSqlServer(connectionString));
	}

	/// <summary>
	/// تسجيل PostgreSQL
	/// </summary>
	public static IServiceCollection AddACommercePostgreSQL(
		this IServiceCollection services,
		string connectionString)
	{
		return services.AddACommerceDbContext(options =>
			options.UseNpgsql(connectionString));
	}

	/// <summary>
	/// تسجيل SQLite
	/// </summary>
	public static IServiceCollection AddACommerceSQLite(
		this IServiceCollection services,
		string connectionString)
	{
		return services.AddACommerceDbContext(options =>
			options.UseSqlite(connectionString));
	}
}
