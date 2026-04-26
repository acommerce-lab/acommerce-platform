// Providers/IFileProvider.cs
using ACommerce.Files.Abstractions.Enums;
using ACommerce.Files.Abstractions.Models;
using FileInfo = ACommerce.Files.Abstractions.Models.FileInfo;

namespace ACommerce.Files.Abstractions.Providers;

// Providers/IFileProvider.cs
/// <summary>
/// ???? ??????? - ???? ???? ???? Storage + Processing + Metadata
/// </summary>
public interface IFileProvider
{
	/// <summary>
	/// ??? ??????
	/// </summary>
	string ProviderName { get; }

	/// <summary>
	/// ??? ???
	/// </summary>
	Task<UploadResult> UploadAsync(
		UploadRequest request,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// ??? ??????? ???
	/// </summary>
	Task<FileInfo?> GetFileAsync(
		string fileId,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// ????? ???
	/// </summary>
	Task<Stream?> DownloadAsync(
		string fileId,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// ??? ???
	/// </summary>
	Task<bool> DeleteAsync(
		string fileId,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// ??? ????? ????????
	/// </summary>
	Task<List<FileInfo>> GetUserFilesAsync(
		string userId,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// ????? ?? ???????
	/// </summary>
	Task<List<FileInfo>> SearchAsync(
		string query,
		FileType? fileType = null,
		CancellationToken cancellationToken = default);
}

