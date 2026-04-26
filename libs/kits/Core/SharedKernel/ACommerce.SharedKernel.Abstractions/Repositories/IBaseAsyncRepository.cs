using ACommerce.SharedKernel.Abstractions.Entities;
using ACommerce.SharedKernel.Abstractions.Queries;
using System.Linq.Expressions;

namespace ACommerce.SharedKernel.Abstractions.Repositories;

/// <summary>
/// المستودع الأساسي لجميع الكيانات
/// </summary>
public interface IBaseAsyncRepository<T> where T : class, IBaseEntity
{
	// ====================================================================================
	// القراءة الأساسية
	// ====================================================================================

	/// <summary>
	/// الحصول على كيان بواسطة المعرف
	/// </summary>
	Task<T?> GetByIdAsync(
		Guid id,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// الحصول على كيان بواسطة المعرف مع خيار تضمين المحذوفات
	/// </summary>
	Task<T?> GetByIdAsync(
		Guid id,
		bool includeDeleted,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// الحصول على جميع الكيانات
	/// </summary>
	Task<IReadOnlyList<T>> ListAllAsync(
		CancellationToken cancellationToken = default);

	/// <summary>
	/// الحصول على جميع الكيانات مع خيار تضمين المحذوفات
	/// </summary>
	Task<IReadOnlyList<T>> ListAllAsync(
		bool includeDeleted,
		CancellationToken cancellationToken = default);

	// ====================================================================================
	// البحث والتصفية المتقدمة
	// ====================================================================================

	/// <summary>
	/// الحصول على كيانات بشرط معين
	/// </summary>
	Task<IReadOnlyList<T>> GetAllWithPredicateAsync(
		Expression<Func<T, bool>>? predicate = null,
		bool includeDeleted = false,
		params string[] includeProperties);

	/// <summary>
	/// الحصول على نتائج مقسمة إلى صفحات
	/// </summary>
	Task<PagedResult<T>> GetPagedAsync(
		int pageNumber = 1,
		int pageSize = 10,
		Expression<Func<T, bool>>? predicate = null,
		Expression<Func<T, object>>? orderBy = null,
		bool ascending = true,
		bool includeDeleted = false,
		params string[] includeProperties);

	/// <summary>
	/// البحث الذكي الشامل
	/// </summary>
	Task<PagedResult<T>> SmartSearchAsync(
		SmartSearchRequest request,
		CancellationToken cancellationToken = default);

	// ====================================================================================
	// الإضافة
	// ====================================================================================

	/// <summary>
	/// إضافة كيان جديد
	/// </summary>
	Task<T> AddAsync(
		T entity,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// إضافة مجموعة من الكيانات
	/// </summary>
	Task<IEnumerable<T>> AddRangeAsync(
		IEnumerable<T> entities,
		CancellationToken cancellationToken = default);

	// ====================================================================================
	// التحديث
	// ====================================================================================

	/// <summary>
	/// تحديث كيان
	/// </summary>
	Task UpdateAsync(
		T entity,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// تحديث جزئي لخصائص محددة
	/// </summary>
	Task PartialUpdateAsync(
		Guid id,
		Dictionary<string, object> updates,
		CancellationToken cancellationToken = default);

	// ====================================================================================
	// الحذف
	// ====================================================================================

	/// <summary>
	/// حذف نهائي لكيان (Hard Delete)
	/// </summary>
	Task DeleteAsync(
		T entity,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// حذف نهائي بواسطة المعرف (Hard Delete)
	/// </summary>
	Task DeleteAsync(
		Guid id,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// حذف منطقي لكيان (Soft Delete)
	/// </summary>
	Task SoftDeleteAsync(
		T entity,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// حذف منطقي بواسطة المعرف (Soft Delete)
	/// </summary>
	Task SoftDeleteAsync(
		Guid id,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// استعادة كيان محذوف منطقياً
	/// </summary>
	Task RestoreAsync(
		Guid id,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// حذف مجموعة من الكيانات
	/// </summary>
	Task DeleteRangeAsync(
		IEnumerable<T> entities,
		bool softDelete = true,
		CancellationToken cancellationToken = default);

	// ====================================================================================
	// الإحصائيات والفحص
	// ====================================================================================

	/// <summary>
	/// عد الكيانات
	/// </summary>
	Task<int> CountAsync(
		Expression<Func<T, bool>>? predicate = null,
		bool includeDeleted = false,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// التحقق من وجود كيان
	/// </summary>
	Task<bool> ExistsAsync(
		Expression<Func<T, bool>> predicate,
		bool includeDeleted = false,
		CancellationToken cancellationToken = default);
}
