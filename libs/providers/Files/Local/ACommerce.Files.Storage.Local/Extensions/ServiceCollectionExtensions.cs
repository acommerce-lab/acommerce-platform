// Extensions/ServiceCollectionExtensions.cs
using ACommerce.Files.Abstractions.Providers;
using ACommerce.Files.Storage.Local.Configuration;
using ACommerce.Files.Storage.Local.Providers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.Files.Storage.Local.Extensions;

public static class ServiceCollectionExtensions
{
	/// <summary>
	/// ????? Local Storage
	/// </summary>
	public static IServiceCollection AddLocalFileStorage(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		// Options
		services.AddOptions<LocalStorageOptions>()
			.Bind(configuration.GetSection(LocalStorageOptions.SectionName))
			.ValidateDataAnnotations()
			.ValidateOnStart();

		// Storage Provider
		services.AddSingleton<IStorageProvider, LocalStorageProvider>();

		// File Provider (In-Memory ??????? ??????)
		services.AddSingleton<IFileProvider, InMemoryFileProvider>();

		return services;
	}

	/// <summary>
	/// ????? Local Storage ?????? ????
	/// </summary>
	public static IServiceCollection AddLocalFileStorage(
		this IServiceCollection services,
		Action<LocalStorageOptions> configure)
	{
		// Options
		services.AddOptions<LocalStorageOptions>()
			.Configure(configure)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		// Storage Provider
		services.AddSingleton<IStorageProvider, LocalStorageProvider>();

		// File Provider
		services.AddSingleton<IFileProvider, InMemoryFileProvider>();

		return services;
	}
}

