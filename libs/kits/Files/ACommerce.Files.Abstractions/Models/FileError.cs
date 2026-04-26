// Models/FileError.cs
namespace ACommerce.Files.Abstractions.Models;

public record FileError
{
	public required string Code { get; init; }
	public required string Message { get; init; }
	public string? Details { get; init; }
}

