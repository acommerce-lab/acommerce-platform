namespace ACommerce.SharedKernel.Abstractions.Results;

/// <summary>
/// نتيجة عملية مع إمكانية حمل البيانات أو الأخطاء
/// </summary>
public class OperationResult<T>
{
	/// <summary>
	/// هل نجحت العملية؟
	/// </summary>
	public bool Success { get; set; }

	/// <summary>
	/// البيانات الناتجة (في حالة النجاح)
	/// </summary>
	public T? Data { get; set; }

	/// <summary>
	/// رسالة الخطأ (في حالة الفشل)
	/// </summary>
	public string? ErrorMessage { get; set; }

	/// <summary>
	/// أخطاء التحقق من الصحة
	/// </summary>
	public List<string> ValidationErrors { get; set; } = new();

	/// <summary>
	/// كود الخطأ (اختياري)
	/// </summary>
	public string? ErrorCode { get; set; }

	/// <summary>
	/// معلومات إضافية
	/// </summary>
	public Dictionary<string, object>? Metadata { get; set; }

	/// <summary>
	/// إنشاء نتيجة نجاح
	/// </summary>
	public static OperationResult<T> SuccessResult(T data)
	{
		return new OperationResult<T>
		{
			Success = true,
			Data = data
		};
	}

	/// <summary>
	/// إنشاء نتيجة فشل
	/// </summary>
	public static OperationResult<T> FailureResult(string errorMessage, string? errorCode = null)
	{
		return new OperationResult<T>
		{
			Success = false,
			ErrorMessage = errorMessage,
			ErrorCode = errorCode
		};
	}

	/// <summary>
	/// إنشاء نتيجة فشل التحقق من الصحة
	/// </summary>
	public static OperationResult<T> ValidationFailure(List<string> validationErrors)
	{
		return new OperationResult<T>
		{
			Success = false,
			ValidationErrors = validationErrors,
			ErrorMessage = "Validation failed"
		};
	}

	/// <summary>
	/// إنشاء نتيجة فشل التحقق من الصحة (من Dictionary)
	/// </summary>
	public static OperationResult<T> ValidationFailure(Dictionary<string, string> validationErrors)
	{
		return new OperationResult<T>
		{
			Success = false,
			ValidationErrors = validationErrors.Select(kvp => $"{kvp.Key}: {kvp.Value}").ToList(),
			ErrorMessage = "Validation failed"
		};
	}
}

/// <summary>
/// نتيجة عملية بدون بيانات
/// </summary>
public class OperationResult
{
	/// <summary>
	/// هل نجحت العملية؟
	/// </summary>
	public bool Success { get; set; }

	/// <summary>
	/// رسالة الخطأ (في حالة الفشل)
	/// </summary>
	public string? ErrorMessage { get; set; }

	/// <summary>
	/// أخطاء التحقق من الصحة
	/// </summary>
	public List<string> ValidationErrors { get; set; } = new();

	/// <summary>
	/// كود الخطأ (اختياري)
	/// </summary>
	public string? ErrorCode { get; set; }

	/// <summary>
	/// معلومات إضافية
	/// </summary>
	public Dictionary<string, object>? Metadata { get; set; }

	/// <summary>
	/// إنشاء نتيجة نجاح
	/// </summary>
	public static OperationResult SuccessResult()
	{
		return new OperationResult
		{
			Success = true
		};
	}

	/// <summary>
	/// إنشاء نتيجة فشل
	/// </summary>
	public static OperationResult FailureResult(string errorMessage, string? errorCode = null)
	{
		return new OperationResult
		{
			Success = false,
			ErrorMessage = errorMessage,
			ErrorCode = errorCode
		};
	}

	/// <summary>
	/// إنشاء نتيجة فشل التحقق من الصحة
	/// </summary>
	public static OperationResult ValidationFailure(List<string> validationErrors)
	{
		return new OperationResult
		{
			Success = false,
			ValidationErrors = validationErrors,
			ErrorMessage = "Validation failed"
		};
	}

	/// <summary>
	/// إنشاء نتيجة فشل التحقق من الصحة (من Dictionary)
	/// </summary>
	public static OperationResult ValidationFailure(Dictionary<string, string> validationErrors)
	{
		return new OperationResult
		{
			Success = false,
			ValidationErrors = validationErrors.Select(kvp => $"{kvp.Key}: {kvp.Value}").ToList(),
			ErrorMessage = "Validation failed"
		};
	}
}
