namespace ACommerce.SharedKernel.Abstractions.Entities;

/// <summary>
/// الواجهة الأساسية لجميع الكيانات في النظام
/// </summary>
public interface IBaseEntity
{
	/// <summary>
	/// المعرف الفريد للكيان
	/// </summary>
	Guid Id { get; set; }

	/// <summary>
	/// تاريخ الإنشاء (UTC)
	/// </summary>
	DateTime CreatedAt { get; set; }

	/// <summary>
	/// تاريخ آخر تحديث (UTC)
	/// </summary>
	DateTime? UpdatedAt { get; set; }

	/// <summary>
	/// علامة الحذف المنطقي (Soft Delete)
	/// </summary>
	bool IsDeleted { get; set; }
}
