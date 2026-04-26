// Exceptions/FileException.cs
namespace ACommerce.Files.Abstractions.Exceptions;

public class FileException : Exception
{
	public string ErrorCode { get; }

	public FileException(string errorCode, string message)
		: base(message)
	{
		ErrorCode = errorCode;
	}

	public FileException(string errorCode, string message, Exception innerException)
		: base(message, innerException)
	{
		ErrorCode = errorCode;
	}
}

