// Models/UploadRequest.cs
using System.ComponentModel.DataAnnotations.Schema;

namespace ACommerce.Files.Abstractions.Models;

// Models/UploadRequest.cs
public record UploadRequest
{
	public required Stream FileStream { get; init; }
	public required string FileName { get; init; }
	public required string ContentType { get; init; }
	public string? OwnerId { get; init; }
	public string? Directory { get; init; }
	public bool GenerateThumbnail { get; init; } = true;
    [NotMapped] public Dictionary<string, string>? Metadata { get; init; }
}

