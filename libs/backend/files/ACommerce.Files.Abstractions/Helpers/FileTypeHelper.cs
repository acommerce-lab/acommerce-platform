// Helpers/FileTypeHelper.cs
using ACommerce.Files.Abstractions.Enums;

namespace ACommerce.Files.Abstractions.Helpers;

public static class FileTypeHelper
{
	private static readonly Dictionary<string, FileType> _mimeTypeMap = new()
	{
		// Images
		["image/jpeg"] = FileType.Image,
		["image/jpg"] = FileType.Image,
		["image/png"] = FileType.Image,
		["image/gif"] = FileType.Image,
		["image/webp"] = FileType.Image,
		["image/bmp"] = FileType.Image,
		["image/svg+xml"] = FileType.Image,

		// Documents
		["application/pdf"] = FileType.Document,
		["application/msword"] = FileType.Document,
		["application/vnd.openxmlformats-officedocument.wordprocessingml.document"] = FileType.Document,
		["application/vnd.ms-excel"] = FileType.Document,
		["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"] = FileType.Document,
		["text/plain"] = FileType.Document,

		// Videos
		["video/mp4"] = FileType.Video,
		["video/mpeg"] = FileType.Video,
		["video/webm"] = FileType.Video,
		["video/quicktime"] = FileType.Video,

		// Audio
		["audio/mpeg"] = FileType.Audio,
		["audio/wav"] = FileType.Audio,
		["audio/ogg"] = FileType.Audio,

		// Archives
		["application/zip"] = FileType.Archive,
		["application/x-rar-compressed"] = FileType.Archive,
		["application/x-7z-compressed"] = FileType.Archive,
	};

	public static FileType GetFileType(string contentType)
	{
		if (string.IsNullOrWhiteSpace(contentType))
			return FileType.Unknown;

		return _mimeTypeMap.TryGetValue(contentType.ToLowerInvariant(), out var fileType)
			? fileType
			: FileType.Other;
	}

	public static bool IsImage(string contentType)
	{
		return GetFileType(contentType) == FileType.Image;
	}

	public static bool IsDocument(string contentType)
	{
		return GetFileType(contentType) == FileType.Document;
	}

	public static bool IsVideo(string contentType)
	{
		return GetFileType(contentType) == FileType.Video;
	}
}

