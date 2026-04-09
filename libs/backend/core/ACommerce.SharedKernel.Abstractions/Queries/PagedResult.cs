namespace ACommerce.SharedKernel.Abstractions.Queries;

/// <summary>
/// نتيجة مقسمة إلى صفحات
/// </summary>
public class PagedResult<T>
{
	/// <summary>
	/// العناصر في الصفحة الحالية
	/// </summary>
	public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();

	/// <summary>
	/// العدد الإجمالي للعناصر
	/// </summary>
	public int TotalCount { get; set; }

	/// <summary>
	/// رقم الصفحة الحالية
	/// </summary>
	public int PageNumber { get; set; }

	/// <summary>
	/// حجم الصفحة
	/// </summary>
	public int PageSize { get; set; }

	/// <summary>
	/// العدد الإجمالي للصفحات
	/// </summary>
	public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;

	/// <summary>
	/// هل توجد صفحة تالية؟
	/// </summary>
	public bool HasNextPage => PageNumber < TotalPages;

	/// <summary>
	/// هل توجد صفحة سابقة؟
	/// </summary>
	public bool HasPreviousPage => PageNumber > 1;

	/// <summary>
	/// رقم الصفحة التالية (null إذا لم توجد)
	/// </summary>
	public int? NextPageNumber => HasNextPage ? PageNumber + 1 : null;

	/// <summary>
	/// رقم الصفحة السابقة (null إذا لم توجد)
	/// </summary>
	public int? PreviousPageNumber => HasPreviousPage ? PageNumber - 1 : null;

	/// <summary>
	/// معلومات إضافية (اختياري)
	/// </summary>
	public Dictionary<string, object>? Metadata { get; set; }

	/// <summary>
	/// إنشاء نتيجة فارغة
	/// </summary>
	public static PagedResult<T> Empty(int pageNumber = 1, int pageSize = 10)
	{
		return new PagedResult<T>
		{
			Items = Array.Empty<T>(),
			TotalCount = 0,
			PageNumber = pageNumber,
			PageSize = pageSize
		};
	}

	/// <summary>
	/// إنشاء نتيجة من قائمة
	/// </summary>
	public static PagedResult<T> Create(
		IReadOnlyList<T> items,
		int totalCount,
		int pageNumber,
		int pageSize)
	{
		return new PagedResult<T>
		{
			Items = items,
			TotalCount = totalCount,
			PageNumber = pageNumber,
			PageSize = pageSize
		};
	}
}
