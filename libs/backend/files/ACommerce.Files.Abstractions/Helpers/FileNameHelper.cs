// Helpers/FileNameHelper.cs
namespace ACommerce.Files.Abstractions.Helpers;

// Helpers/FileNameHelper.cs
public static class FileNameHelper
{
	/// <summary>
	/// ????? ??? ??? ????
	/// </summary>
	public static string GenerateUniqueFileName(string originalFileName)
	{
		var extension = Path.GetExtension(originalFileName);
		var fileId = Guid.NewGuid().ToString("N");
		return $"{fileId}{extension}";
	}

	/// <summary>
	/// ????? ??? ?????
	/// </summary>
	public static string SanitizeFileName(string fileName)
	{
		var invalidChars = Path.GetInvalidFileNameChars();
		var sanitized = string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
		return sanitized;
	}

	/// <summary>
	/// ?????? ??? ????????
	/// </summary>
	public static string GetExtension(string fileName)
	{
		return Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant();
	}
}

