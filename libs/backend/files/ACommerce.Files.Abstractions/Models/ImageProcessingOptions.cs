// Models/ImageProcessingOptions.cs
using ACommerce.Files.Abstractions.Enums;

namespace ACommerce.Files.Abstractions.Models;

// Models/ImageProcessingOptions.cs
public record ImageProcessingOptions
{
	public int? Width { get; init; }
	public int? Height { get; init; }
	public ImageFormat? Format { get; init; }
	public int Quality { get; init; } = 85;
	public bool MaintainAspectRatio { get; init; } = true;
	public string? WatermarkText { get; init; }
}

