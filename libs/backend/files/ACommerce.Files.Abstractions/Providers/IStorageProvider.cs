// Providers/IStorageProvider.cs
using ACommerce.Files.Abstractions.Enums;

namespace ACommerce.Files.Abstractions.Providers;

/// <summary>
/// ???? ??????? - ???? ??????? ??????????
/// </summary>
public interface IStorageProvider
{
	/// <summary>
	/// ??? ??????
	/// </summary>
	string ProviderName { get; }

	/// <summary>
	/// ??? ???????
	/// </summary>
	StorageType StorageType { get; }

	/// <summary>
	/// ??? ???
	/// </summary>
	Task<string> SaveAsync(
		Stream stream,
		string fileName,
		string? directory = null,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// ??? ???
	/// </summary>
	Task<Stream?> GetAsync(
		string filePath,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// ??? ???
	/// </summary>
	Task<bool> DeleteAsync(
		string filePath,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// ?????? ?? ???? ???
	/// </summary>
	Task<bool> ExistsAsync(
		string filePath,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// ?????? ??? ???? ???
	/// </summary>
	Task<string> GetPublicUrlAsync(
		string filePath,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// ?????? ??? ???? ????
	/// </summary>
	Task<string> GetSignedUrlAsync(
		string filePath,
		TimeSpan expiration,
		CancellationToken cancellationToken = default);
}

