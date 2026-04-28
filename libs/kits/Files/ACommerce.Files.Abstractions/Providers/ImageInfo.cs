// Providers/ImageInfo.cs
namespace ACommerce.Files.Abstractions.Providers;

public record ImageInfo
{
	public int Width { get; init; }
	public int Height { get; init; }
	public string Format { get; init; } = default!;
	public long SizeInBytes { get; init; }
}

