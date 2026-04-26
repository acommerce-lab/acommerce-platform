namespace ACommerce.SharedKernel.Abstractions.Exceptions;

/// <summary>
/// استثناء عام للنطاق (Domain)
/// </summary>
public class DomainException : Exception
{
	public string ErrorCode { get; }

	public DomainException(string errorCode, string message)
		: base(message)
	{
		ErrorCode = errorCode;
	}

	public DomainException(string errorCode, string message, Exception innerException)
		: base(message, innerException)
	{
		ErrorCode = errorCode;
	}
}

/// <summary>
/// استثناء عند عدم العثور على الكيان
/// </summary>
public class EntityNotFoundException : DomainException
{
	public EntityNotFoundException(string entityName, Guid id)
		: base("ENTITY_NOT_FOUND", $"{entityName} with id {id} was not found.")
	{
	}

	public EntityNotFoundException(string entityName, string identifier)
		: base("ENTITY_NOT_FOUND", $"{entityName} with identifier '{identifier}' was not found.")
	{
	}
}

/// <summary>
/// استثناء التحقق من صحة البيانات
/// </summary>
public class ValidationException : DomainException
{
	public Dictionary<string, string> Errors { get; }

	public ValidationException(Dictionary<string, string> errors)
		: base("VALIDATION_ERROR", "One or more validation errors occurred.")
	{
		Errors = errors;
	}

	public ValidationException(string field, string error)
		: base("VALIDATION_ERROR", error)
	{
		Errors = new Dictionary<string, string> { { field, error } };
	}
}

/// <summary>
/// استثناء الصلاحيات
/// </summary>
public class UnauthorizedException : DomainException
{
	public UnauthorizedException(string message)
		: base("UNAUTHORIZED", message)
	{
	}
}

/// <summary>
/// استثناء العمليات المتزامنة
/// </summary>
public class ConcurrencyException : DomainException
{
	public ConcurrencyException(string entityName, Guid id)
		: base("CONCURRENCY_ERROR", $"{entityName} with id {id} was modified by another user.")
	{
	}
}
