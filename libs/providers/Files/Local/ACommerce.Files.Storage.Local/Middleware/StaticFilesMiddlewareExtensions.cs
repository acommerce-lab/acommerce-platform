// Middleware/StaticFilesMiddlewareExtensions.cs
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.FileProviders;

namespace ACommerce.Files.Storage.Local.Middleware;

public static class StaticFilesMiddlewareExtensions
{
	/// <summary>
	/// ????? ???? ??????? ???????
	/// </summary>
	public static IApplicationBuilder UseLocalFileStorage(
		this IApplicationBuilder app,
		string requestPath = "/files",
		string physicalPath = "uploads")
	{
		app.UseStaticFiles(new StaticFileOptions
		{
			FileProvider = new PhysicalFileProvider(
				Path.Combine(Directory.GetCurrentDirectory(), physicalPath)),
			RequestPath = requestPath
		});

		return app;
	}
}

