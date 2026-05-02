using ACommerce.SharedKernel.Domain.Entities;
using ACommerce.SharedKernel.Repositories.Queries;
using System.Linq.Expressions;

namespace ACommerce.SharedKernel.Repositories.Interfaces;

/// <summary>
/// �������� ������� ����� ��������
/// </summary>
public interface IBaseAsyncRepository<T> where T : class, IBaseEntity
{
	// ====================================================================================
	// ������� ��������
	// ====================================================================================

	/// <summary>
	/// ������ ��� ���� ������ ������
	/// </summary>
	Task<T?> GetByIdAsync(
		Guid id,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// ������ ��� ���� ������ ������ �� ���� ����� ���������
	/// </summary>
	Task<T?> GetByIdAsync(
		Guid id,
		bool includeDeleted,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// ������ ��� ���� ��������
	/// </summary>
	Task<IReadOnlyList<T>> ListAllAsync(
		CancellationToken cancellationToken = default);

	/// <summary>
	/// ������ ��� ���� �������� �� ���� ����� ���������
	/// </summary>
	Task<IReadOnlyList<T>> ListAllAsync(
		bool includeDeleted,
		CancellationToken cancellationToken = default);

	// ====================================================================================
	// ����� �������� ��������
	// ====================================================================================

	/// <summary>
	/// ������ ��� ������ ���� ����
	/// </summary>
	Task<IReadOnlyList<T>> GetAllWithPredicateAsync(
		Expression<Func<T, bool>>? predicate = null,
		bool includeDeleted = false,
		params string[] includeProperties);

	/// <summary>
	/// ������ ��� ����� ����� ��� �����
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
	/// ����� ����� ������
	/// </summary>
	Task<PagedResult<T>> SmartSearchAsync(
		SmartSearchRequest request,
		CancellationToken cancellationToken = default);

	// ====================================================================================
	// �������
	// ====================================================================================

	/// <summary>
	/// ����� ���� ����
	/// </summary>
	Task<T> AddAsync(
		T entity,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// (F6) إضافة كيان دون استدعاء SaveChanges. مفيد عندما تحتاج العمليّة
	/// إلى ضمّ عدّة كيانات (مثل Message + Conversation) في معاملة واحدة:
	/// كلّ AddNoSaveAsync يضيف للـ context tracked فقط، ثم يُستدعى
	/// <see cref="ACommerce.SharedKernel.Repositories.Interfaces.IUnitOfWork.SaveChangesAsync"/>
	/// مرّة واحدة في نهاية الـ Execute body فيحفظ الكلّ ذرّيّاً.
	/// </summary>
	Task<T> AddNoSaveAsync(
		T entity,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// ����� ������ �� ��������
	/// </summary>
	Task<IEnumerable<T>> AddRangeAsync(
		IEnumerable<T> entities,
		CancellationToken cancellationToken = default);

	// ====================================================================================
	// �������
	// ====================================================================================

	/// <summary>
	/// ����� ����
	/// </summary>
	Task UpdateAsync(
		T entity,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// ����� ���� ������ �����
	/// </summary>
	Task PartialUpdateAsync(
		Guid id,
		Dictionary<string, object> updates,
		CancellationToken cancellationToken = default);

	// ====================================================================================
	// �����
	// ====================================================================================

	/// <summary>
	/// ��� ����� ����� (Hard Delete)
	/// </summary>
	Task DeleteAsync(
		T entity,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// ��� ����� ������ ������ (Hard Delete)
	/// </summary>
	Task DeleteAsync(
		Guid id,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// ��� ����� ����� (Soft Delete)
	/// </summary>
	Task SoftDeleteAsync(
		T entity,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// ��� ����� ������ ������ (Soft Delete)
	/// </summary>
	Task SoftDeleteAsync(
		Guid id,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// ������� ���� ����� �������
	/// </summary>
	Task RestoreAsync(
		Guid id,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// ��� ������ �� ��������
	/// </summary>
	Task DeleteRangeAsync(
		IEnumerable<T> entities,
		bool softDelete = true,
		CancellationToken cancellationToken = default);

	// ====================================================================================
	// ���������� ������
	// ====================================================================================

	/// <summary>
	/// �� ��������
	/// </summary>
	Task<int> CountAsync(
		Expression<Func<T, bool>>? predicate = null,
		bool includeDeleted = false,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// ������ �� ���� ����
	/// </summary>
	Task<bool> ExistsAsync(
		Expression<Func<T, bool>> predicate,
		bool includeDeleted = false,
		CancellationToken cancellationToken = default);
}
