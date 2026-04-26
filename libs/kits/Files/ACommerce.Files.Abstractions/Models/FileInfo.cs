// Models/FileInfo.cs
using ACommerce.Files.Abstractions.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace ACommerce.Files.Abstractions.Models;

public record FileInfo
{
	public required string FileId { get; init; }
	public required string FileName { get; init; }
	public required string ContentType { get; init; }
	public required long SizeInBytes { get; init; }
	public required FileType FileType { get; init; }
	public required string StoragePath { get; init; }
	public required string PublicUrl { get; init; }
	public string? ThumbnailUrl { get; init; }
	public int? Width { get; init; }
	public int? Height { get; init; }
	public string? OwnerId { get; init; }
    [NotMapped] public Dictionary<string, string> Metadata { get; init; } = new();
	public DateTimeOffset UploadedAt { get; init; }
}

