// Exceptions/ImageProcessingException.cs
namespace ACommerce.Files.Abstractions.Exceptions;

// Exceptions/ImageProcessingException.cs
public class ImageProcessingException : FileException
{
	public ImageProcessingException(string errorCode, string message)
		: base(errorCode, message)
	{
	}

	public ImageProcessingException(string errorCode, string message, Exception innerException)
		: base(errorCode, message, innerException)
	{
	}
}

