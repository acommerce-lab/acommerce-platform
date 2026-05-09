// Providers/LocalStorageProvider.cs
using ACommerce.Files.Abstractions.Enums;
using ACommerce.Files.Abstractions.Exceptions;
using ACommerce.Files.Abstractions.Helpers;
using ACommerce.Files.Abstractions.Providers;
using ACommerce.Files.Storage.Local.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ACommerce.Files.Storage.Local.Providers;

public class LocalStorageProvider : IStorageProvider
{
	private readonly LocalStorageOptions _options;
	private readonly ILogger<LocalStorageProvider> _logger;

	public string ProviderName => "Local";
	public StorageType StorageType => StorageType.Local;

	public LocalStorageProvider(
		IOptions<LocalStorageOptions> options,
		ILogger<LocalStorageProvider> logger)
	{
		_options = options.Value ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));

		EnsureRootDirectoryExists();
	}

	public async Task<string> SaveAsync(
		Stream stream,
		string fileName,
		string? directory = null,
		CancellationToken cancellationToken = default)
	{
		try
		{
			// ????? ??? ????
			var uniqueFileName = FileNameHelper.GenerateUniqueFileName(fileName);

			// ????? ??????
			var relativePath = BuildRelativePath(uniqueFileName, directory);
			var fullPath = Path.Combine(_options.RootPath, relativePath);

			// ?????? ?? ???? ??????
			var directoryPath = Path.GetDirectoryName(fullPath);
			if (!string.IsNullOrEmpty(directoryPath))
			{
				Directory.CreateDirectory(directoryPath);
			}

			// ??? ?????
			await using var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write);
			await stream.CopyToAsync(fileStream, cancellationToken);

			_logger.LogInformation("File saved successfully: {Path}", relativePath);

			return relativePath;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to save file: {FileName}", fileName);
			throw new StorageException("SAVE_FAILED", "Failed to save file", ex);
		}
	}

	public async Task<Stream?> GetAsync(
		string filePath,
		CancellationToken cancellationToken = default)
	{
		try
		{
			var fullPath = Path.Combine(_options.RootPath, filePath);

			if (!File.Exists(fullPath))
			{
				_logger.LogWarning("File not found: {Path}", filePath);
				return null;
			}

			var memoryStream = new MemoryStream();
			await using var fileStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read);
			await fileStream.CopyToAsync(memoryStream, cancellationToken);
			memoryStream.Position = 0;

			return memoryStream;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to get file: {Path}", filePath);
			throw new StorageException("GET_FAILED", "Failed to retrieve file", ex);
		}
	}

	public Task<bool> DeleteAsync(
		string filePath,
		CancellationToken cancellationToken = default)
	{
		try
		{
			var fullPath = Path.Combine(_options.RootPath, filePath);

			if (!File.Exists(fullPath))
			{
				_logger.LogWarning("File not found for deletion: {Path}", filePath);
				return Task.FromResult(false);
			}

			File.Delete(fullPath);

			_logger.LogInformation("File deleted successfully: {Path}", filePath);

			return Task.FromResult(true);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to delete file: {Path}", filePath);
			throw new StorageException("DELETE_FAILED", "Failed to delete file", ex);
		}
	}

	public Task<bool> ExistsAsync(
		string filePath,
		CancellationToken cancellationToken = default)
	{
		try
		{
			var fullPath = Path.Combine(_options.RootPath, filePath);
			return Task.FromResult(File.Exists(fullPath));
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to check file existence: {Path}", filePath);
			return Task.FromResult(false);
		}
	}

	public Task<string> GetPublicUrlAsync(
		string filePath,
		CancellationToken cancellationToken = default)
	{
		// ????? ??? backslashes ?????????? ?? forward slashes
		var normalizedPath = filePath.Replace("\\", "/");
		var url = $"{_options.BaseUrl.TrimEnd('/')}/{normalizedPath}";
		return Task.FromResult(url);
	}

	public Task<string> GetSignedUrlAsync(
		string filePath,
		TimeSpan expiration,
		CancellationToken cancellationToken = default)
	{
		// Local storage ?? ???? signed URLs ???? ?????
		// ???? ????? signed URLs ??? middleware ?? ASP.NET Core
		_logger.LogWarning("Signed URLs are not natively supported in local storage");
		return GetPublicUrlAsync(filePath, cancellationToken);
	}

	private string BuildRelativePath(string fileName, string? customDirectory)
	{
		if (!string.IsNullOrEmpty(customDirectory))
		{
			return Path.Combine(customDirectory, fileName);
		}

		if (_options.UseDirectoryStructure)
		{
			var datePath = DateTime.UtcNow.ToString(_options.DirectoryFormat);
			return Path.Combine(datePath, fileName);
		}

		return fileName;
	}

	private void EnsureRootDirectoryExists()
	{
		if (!Directory.Exists(_options.RootPath))
		{
			Directory.CreateDirectory(_options.RootPath);
			_logger.LogInformation("Created root directory: {Path}", _options.RootPath);
		}
	}
}

