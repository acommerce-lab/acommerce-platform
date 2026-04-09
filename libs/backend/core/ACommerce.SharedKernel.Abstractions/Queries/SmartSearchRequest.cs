namespace ACommerce.SharedKernel.Abstractions.Queries;

/// <summary>
/// طلب البحث الذكي مع التصفية والترتيب والتصفح
/// </summary>
public class SmartSearchRequest
{
	/// <summary>
	/// مصطلح البحث النصي (يبحث في كل الخصائص النصية)
	/// </summary>
	public string? SearchTerm { get; set; }

	/// <summary>
	/// قائمة الفلاتر المتقدمة
	/// </summary>
	public List<FilterItem>? Filters { get; set; }

	/// <summary>
	/// رقم الصفحة (يبدأ من 1)
	/// </summary>
	public int PageNumber { get; set; } = 1;

	/// <summary>
	/// حجم الصفحة
	/// </summary>
	public int PageSize { get; set; } = 10;

	/// <summary>
	/// اسم الخاصية للترتيب
	/// </summary>
	public string? OrderBy { get; set; }

	/// <summary>
	/// الترتيب تصاعدي أم تنازلي
	/// </summary>
	public bool Ascending { get; set; } = true;

	/// <summary>
	/// الخصائص المراد تضمينها (للـ Navigation Properties)
	/// </summary>
	public List<string>? IncludeProperties { get; set; }

	/// <summary>
	/// تضمين العناصر المحذوفة (Soft Delete)
	/// </summary>
	public bool IncludeDeleted { get; set; } = false;

	/// <summary>
	/// التحقق من صحة الطلب
	/// </summary>
	public bool IsValid()
	{
		if (PageNumber < 1) return false;
		if (PageSize < 1 || PageSize > 1000) return false;
		return true;
	}
}
