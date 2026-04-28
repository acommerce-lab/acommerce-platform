// Exceptions/StorageException.cs
namespace ACommerce.Files.Abstractions.Exceptions;

// Exceptions/StorageException.cs
public class StorageException : FileException
{
	public StorageException(string errorCode, string message)
		: base(errorCode, message)
	{
	}

	public StorageException(string errorCode, string message, Exception innerException)
		: base(errorCode, message, innerException)
	{
	}
}

