// Providers/IImageProcessor.cs
using ACommerce.Files.Abstractions.Enums;
using ACommerce.Files.Abstractions.Models;

namespace ACommerce.Files.Abstractions.Providers;

// Providers/IImageProcessor.cs
/// <summary>
/// ????? ?????
/// </summary>
public interface IImageProcessor
{
	/// <summary>
	/// ??? ??????
	/// </summary>
	string ProviderName { get; }

	/// <summary>
	/// ????? ??? ??????
	/// </summary>
	Task<Stream> ResizeAsync(
		Stream inputStream,
		int width,
		int height,
		bool maintainAspectRatio = true,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// ????? ???? ?????
	/// </summary>
	Task<Stream> CreateThumbnailAsync(
		Stream inputStream,
		int size = 150,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// ?? ??????
	/// </summary>
	Task<Stream> CropAsync(
		Stream inputStream,
		int x,
		int y,
		int width,
		int height,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// ????? ????? ?????
	/// </summary>
	Task<Stream> AddWatermarkAsync(
		Stream inputStream,
		string watermarkText,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// ????? ??????
	/// </summary>
	Task<Stream> ConvertFormatAsync(
		Stream inputStream,
		ImageFormat format,
		int quality = 85,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// ?????? ?????? ??????? ??????
	/// </summary>
	Task<Stream> ProcessAsync(
		Stream inputStream,
		ImageProcessingOptions options,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// ?????? ??? ??????? ??????
	/// </summary>
	Task<ImageInfo> GetImageInfoAsync(
		Stream inputStream,
		CancellationToken cancellationToken = default);
}

