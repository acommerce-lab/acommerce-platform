// Models/UploadResult.cs
namespace ACommerce.Files.Abstractions.Models;

// Models/UploadResult.cs
public record UploadResult
{
	public required bool Success { get; init; }
	public FileInfo? File { get; init; }
	public FileError? Error { get; init; }
}

