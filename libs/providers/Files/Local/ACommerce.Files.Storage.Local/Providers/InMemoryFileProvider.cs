// Providers/InMemoryFileProvider.cs
using ACommerce.Files.Abstractions.Enums;
using ACommerce.Files.Abstractions.Exceptions;
using ACommerce.Files.Abstractions.Helpers;
using ACommerce.Files.Abstractions.Models;
using ACommerce.Files.Abstractions.Providers;
using ACommerce.Files.Storage.Local.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace ACommerce.Files.Storage.Local.Providers;

// Providers/InMemoryFileProvider.cs
/// <summary>
/// File provider with in-memory metadata storage (??????? ??????)
/// For production, use database implementation
/// </summary>
public class InMemoryFileProvider : IFileProvider
{
	private readonly IStorageProvider _storageProvider;
	private readonly IImageProcessor? _imageProcessor;
	private readonly ILogger<InMemoryFileProvider> _logger;
	private readonly ConcurrentDictionary<string, Abstractions.Models.FileInfo> _files = new();

	public string ProviderName => "InMemory-Local";

	public InMemoryFileProvider(
		IStorageProvider storageProvider,
		ILogger<InMemoryFileProvider> logger,
		IImageProcessor? imageProcessor = null)
	{
		_storageProvider = storageProvider ?? throw new ArgumentNullException(nameof(storageProvider));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_imageProcessor = imageProcessor;
	}

	public async Task<UploadResult> UploadAsync(
		UploadRequest request,
		CancellationToken cancellationToken = default)
	{
		try
		{
			_logger.LogInformation("Uploading file: {FileName}", request.FileName);

			// ?????? ?? ??? ?????
			var fileType = FileTypeHelper.GetFileType(request.ContentType);

			// ??? ????? ??????
			var storagePath = await _storageProvider.SaveAsync(
				request.FileStream,
				request.FileName,
				request.Directory,
				cancellationToken);

			var publicUrl = await _storageProvider.GetPublicUrlAsync(storagePath, cancellationToken);

			var fileId = Guid.NewGuid().ToString("N");

			// ??????? ??????
			int? width = null;
			int? height = null;
			string? thumbnailUrl = null;

			// ?????? ?????
			if (FileTypeHelper.IsImage(request.ContentType) &&
				_imageProcessor != null &&
				request.GenerateThumbnail)
			{
				try
				{
					// ?????? ??? ??????? ??????
					request.FileStream.Position = 0;
					var imageInfo = await _imageProcessor.GetImageInfoAsync(
						request.FileStream,
						cancellationToken);

					width = imageInfo.Width;
					height = imageInfo.Height;

					// ????? thumbnail
					request.FileStream.Position = 0;
					var thumbnailStream = await _imageProcessor.CreateThumbnailAsync(
						request.FileStream,
						150,
						cancellationToken);

					var thumbnailFileName = $"thumb_{request.FileName}";
					var thumbnailPath = await _storageProvider.SaveAsync(
						thumbnailStream,
						thumbnailFileName,
						request.Directory,
						cancellationToken);

					thumbnailUrl = await _storageProvider.GetPublicUrlAsync(
						thumbnailPath,
						cancellationToken);

					await thumbnailStream.DisposeAsync();
				}
				catch (Exception ex)
				{
					_logger.LogWarning(ex, "Failed to process image for {FileName}", request.FileName);
				}
			}

			// ??? ??????? ?????
			var fileInfo = new Abstractions.Models.FileInfo
			{
				FileId = fileId,
				FileName = request.FileName,
				ContentType = request.ContentType,
				SizeInBytes = request.FileStream.Length,
				FileType = fileType,
				StoragePath = storagePath,
				PublicUrl = publicUrl,
				ThumbnailUrl = thumbnailUrl,
				Width = width,
				Height = height,
				OwnerId = request.OwnerId,
				Metadata = request.Metadata ?? new Dictionary<string, string>(),
				UploadedAt = DateTimeOffset.UtcNow
			};

			_files[fileId] = fileInfo;

			_logger.LogInformation("File uploaded successfully: {FileId}", fileId);

			return new UploadResult
			{
				Success = true,
				File = fileInfo
			};
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to upload file: {FileName}", request.FileName);

			return new UploadResult
			{
				Success = false,
				Error = new FileError
				{
					Code = "UPLOAD_FAILED",
					Message = "Failed to upload file",
					Details = ex.Message
				}
			};
		}
	}

	public Task<Abstractions.Models.FileInfo?> GetFileAsync(
		string fileId,
		CancellationToken cancellationToken = default)
	{
		_files.TryGetValue(fileId, out var file);
		return Task.FromResult(file);
	}

	public async Task<Stream?> DownloadAsync(
		string fileId,
		CancellationToken cancellationToken = default)
	{
		var file = await GetFileAsync(fileId, cancellationToken);

		if (file == null)
		{
			return null;
		}

		return await _storageProvider.GetAsync(file.StoragePath, cancellationToken);
	}

	public async Task<bool> DeleteAsync(
		string fileId,
		CancellationToken cancellationToken = default)
	{
		if (!_files.TryRemove(fileId, out var file))
		{
			return false;
		}

		// ??? ????? ??????
		await _storageProvider.DeleteAsync(file.StoragePath, cancellationToken);

		// ??? ??? thumbnail
		if (!string.IsNullOrEmpty(file.ThumbnailUrl))
		{
			try
			{
				var thumbnailPath = file.StoragePath.Replace(
					Path.GetFileName(file.StoragePath),
					$"thumb_{Path.GetFileName(file.StoragePath)}");

				await _storageProvider.DeleteAsync(thumbnailPath, cancellationToken);
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "Failed to delete thumbnail for {FileId}", fileId);
			}
		}

		return true;
	}

	public Task<List<Abstractions.Models.FileInfo>> GetUserFilesAsync(
		string userId,
		CancellationToken cancellationToken = default)
	{
		var files = _files.Values
			.Where(f => f.OwnerId == userId)
			.OrderByDescending(f => f.UploadedAt)
			.ToList();

		return Task.FromResult(files);
	}

	public Task<List<Abstractions.Models.FileInfo>> SearchAsync(
		string query,
		FileType? fileType = null,
		CancellationToken cancellationToken = default)
	{
		var files = _files.Values.AsEnumerable();

		if (!string.IsNullOrWhiteSpace(query))
		{
			files = files.Where(f =>
				f.FileName.Contains(query, StringComparison.OrdinalIgnoreCase));
		}

		if (fileType.HasValue)
		{
			files = files.Where(f => f.FileType == fileType.Value);
		}

		return Task.FromResult(files.OrderByDescending(f => f.UploadedAt).ToList());
	}
}

