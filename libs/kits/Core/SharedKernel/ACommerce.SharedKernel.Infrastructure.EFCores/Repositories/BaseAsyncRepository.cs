using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ACommerce.SharedKernel.Abstractions.Entities;
using ACommerce.SharedKernel.Abstractions.Queries;
using ACommerce.SharedKernel.Abstractions.Repositories;
using System.Linq.Expressions;

namespace ACommerce.SharedKernel.Infrastructure.EFCore.Repositories;

/// <summary>
/// المستودع الأساسي المعتمد على Entity Framework Core
/// </summary>
public class BaseAsyncRepository<T> : IBaseAsyncRepository<T>
        where T : class, IBaseEntity
{
        protected readonly DbContext _context;
        protected readonly DbSet<T> _dbSet;
        protected readonly ILogger<BaseAsyncRepository<T>> _logger;

        public BaseAsyncRepository(
                DbContext context,
                ILogger<BaseAsyncRepository<T>> logger)
        {
                _context = context ?? throw new ArgumentNullException(nameof(context));
                _logger = logger ?? throw new ArgumentNullException(nameof(logger));
                _dbSet = _context.Set<T>();
        }

        // ====================================================================================
        // Helper Methods
        // ====================================================================================

        /// <summary>
        /// تطبيق التضمينات (Include) على الاستعلام
        /// </summary>
        protected virtual IQueryable<T> ApplyIncludes(
                IQueryable<T> query,
                params string[] includeProperties)
        {
                if (includeProperties == null || includeProperties.Length == 0)
                        return query;

                return includeProperties.Aggregate(
                        query,
                        (current, includeProperty) => current.Include(includeProperty));
        }

        /// <summary>
        /// تطبيق فلتر الحذف المنطقي
        /// </summary>
        protected virtual IQueryable<T> ApplySoftDeleteFilter(
                IQueryable<T> query,
                bool includeDeleted)
        {
                return includeDeleted ? query : query.Where(e => !e.IsDeleted);
        }

        // ====================================================================================
        // القراءة الأساسية
        // ====================================================================================

        public virtual async Task<T?> GetByIdAsync(
                Guid id,
                CancellationToken cancellationToken = default)
        {
                return await GetByIdAsync(id, false, cancellationToken);
        }

        public virtual async Task<T?> GetByIdAsync(
                Guid id,
                bool includeDeleted,
                CancellationToken cancellationToken = default)
        {
                _logger.LogDebug("Getting {EntityName} by id {EntityId}", typeof(T).Name, id);

                var query = _dbSet.AsNoTracking();
                query = ApplySoftDeleteFilter(query, includeDeleted);

                return await query.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        }

        /// <summary>
        /// الحصول على كيان بمعرفه مع التتبع (للاستخدام الداخلي في عمليات التحديث)
        /// </summary>
        protected virtual async Task<T?> GetByIdTrackedAsync(
                Guid id,
                bool includeDeleted,
                CancellationToken cancellationToken = default)
        {
                _logger.LogDebug("Getting tracked {EntityName} by id {EntityId}", typeof(T).Name, id);

                IQueryable<T> query = _dbSet;
                query = ApplySoftDeleteFilter(query, includeDeleted);

                return await query.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        }

        public virtual async Task<IReadOnlyList<T>> ListAllAsync(
                CancellationToken cancellationToken = default)
        {
                return await ListAllAsync(false, cancellationToken);
        }

        public virtual async Task<IReadOnlyList<T>> ListAllAsync(
                bool includeDeleted,
                CancellationToken cancellationToken = default)
        {
                _logger.LogDebug("Listing all {EntityName}", typeof(T).Name);

                var query = _dbSet.AsNoTracking();
                query = ApplySoftDeleteFilter(query, includeDeleted);

                return await query.ToListAsync(cancellationToken);
        }

        // ====================================================================================
        // البحث والتصفية المتقدمة
        // ====================================================================================

        public virtual async Task<IReadOnlyList<T>> GetAllWithPredicateAsync(
                Expression<Func<T, bool>>? predicate = null,
                bool includeDeleted = false,
                params string[] includeProperties)
        {
                _logger.LogDebug(
                        "Getting {EntityName} with predicate, includeDeleted: {IncludeDeleted}",
                        typeof(T).Name,
                        includeDeleted);

                IQueryable<T> query = _dbSet.AsNoTracking();

                query = ApplyIncludes(query, includeProperties);
                query = ApplySoftDeleteFilter(query, includeDeleted);

                if (predicate != null)
                        query = query.Where(predicate);

                return await query.ToListAsync();
        }

        public virtual async Task<PagedResult<T>> GetPagedAsync(
                int pageNumber = 1,
                int pageSize = 10,
                Expression<Func<T, bool>>? predicate = null,
                Expression<Func<T, object>>? orderBy = null,
                bool ascending = true,
                bool includeDeleted = false,
                params string[] includeProperties)
        {
                _logger.LogDebug(
                        "Getting paged {EntityName}, page {PageNumber}, size {PageSize}",
                        typeof(T).Name,
                        pageNumber,
                        pageSize);

                IQueryable<T> query = _dbSet.AsNoTracking();

                query = ApplyIncludes(query, includeProperties);
                query = ApplySoftDeleteFilter(query, includeDeleted);

                if (predicate != null)
                        query = query.Where(predicate);

                var totalCount = await query.CountAsync();

                if (orderBy != null)
                        query = ascending ? query.OrderBy(orderBy) : query.OrderByDescending(orderBy);
                else
                        query = query.OrderByDescending(e => e.CreatedAt);

                var items = await query
                        .Skip((pageNumber - 1) * pageSize)
                        .Take(pageSize)
                        .ToListAsync();

                return new PagedResult<T>
                {
                        Items = items,
                        TotalCount = totalCount,
                        PageNumber = pageNumber,
                        PageSize = pageSize
                };
        }

        public virtual async Task<PagedResult<T>> SmartSearchAsync(
                SmartSearchRequest request,
                CancellationToken cancellationToken = default)
        {
                _logger.LogDebug(
                        "Smart searching {EntityName} with page {PageNumber}, size {PageSize}, term: {SearchTerm}",
                        typeof(T).Name,
                        request.PageNumber,
                        request.PageSize,
                        request.SearchTerm);

                IQueryable<T> query = _dbSet.AsNoTracking();

                // تطبيق التضمينات
                if (request.IncludeProperties?.Count > 0)
                        query = ApplyIncludes(query, request.IncludeProperties.ToArray());

                // تطبيق فلتر Soft Delete
                query = ApplySoftDeleteFilter(query, request.IncludeDeleted);

                // البحث الذكي في النصوص
                if (!string.IsNullOrWhiteSpace(request.SearchTerm))
                        query = ApplySmartTextSearch(query, request.SearchTerm);

                // تطبيق الفلاتر المحددة
                if (request.Filters?.Count > 0)
                        query = ApplyAdvancedFilters(query, request.Filters);

                var totalCount = await query.CountAsync(cancellationToken);

                // ترتيب النتائج
                if (!string.IsNullOrWhiteSpace(request.OrderBy))
                        query = ApplyOrdering(query, request.OrderBy, request.Ascending);
                else
                        query = query.OrderByDescending(e => e.CreatedAt);

                // تطبيق التصفح
                var items = await query
                        .Skip((request.PageNumber - 1) * request.PageSize)
                        .Take(request.PageSize)
                        .ToListAsync(cancellationToken);

                return new PagedResult<T>
                {
                        Items = items,
                        TotalCount = totalCount,
                        PageNumber = request.PageNumber,
                        PageSize = request.PageSize,
                        Metadata = new Dictionary<string, object>
                        {
                                ["searchTerm"] = request.SearchTerm ?? string.Empty,
                                ["filtersApplied"] = request.Filters?.Count ?? 0
                        }
                };
        }

        /// <summary>
        /// البحث الذكي في الخصائص النصية
        /// </summary>
        protected virtual IQueryable<T> ApplySmartTextSearch(
                IQueryable<T> query,
                string searchTerm)
        {
                var stringProperties = typeof(T)
                        .GetProperties()
                        .Where(p => p.PropertyType == typeof(string) && p.CanRead)
                        .ToList();

                if (stringProperties.Count == 0)
                        return query;

                var parameter = Expression.Parameter(typeof(T), "x");
                Expression? searchExpression = null;

                foreach (var property in stringProperties)
                {
                        var propertyAccess = Expression.Property(parameter, property);
                        var nullCheck = Expression.NotEqual(propertyAccess, Expression.Constant(null));

                        var toLowerMethod = typeof(string).GetMethod("ToLower", Type.EmptyTypes);
                        var containsMethod = typeof(string).GetMethod("Contains", new[] { typeof(string) });

                        var propertyToLower = Expression.Call(propertyAccess, toLowerMethod!);
                        var searchValue = Expression.Constant(searchTerm.ToLower());
                        var containsCall = Expression.Call(propertyToLower, containsMethod!, searchValue);

                        var safeContains = Expression.AndAlso(nullCheck, containsCall);

                        searchExpression = searchExpression == null
                                ? safeContains
                                : Expression.OrElse(searchExpression, safeContains);
                }

                if (searchExpression != null)
                {
                        var lambda = Expression.Lambda<Func<T, bool>>(searchExpression, parameter);
                        query = query.Where(lambda);
                }

                return query;
        }

        /// <summary>
        /// تطبيق الفلاتر المتقدمة
        /// </summary>
        protected virtual IQueryable<T> ApplyAdvancedFilters(
                IQueryable<T> query,
                List<FilterItem> filters)
        {
                var parameter = Expression.Parameter(typeof(T), "x");
                Expression? filterExpression = null;

                foreach (var filter in filters)
                {
                        var property = typeof(T).GetProperty(filter.PropertyName);
                        if (property == null)
                        {
                                _logger.LogWarning(
                                        "Property {PropertyName} not found on {EntityName}",
                                        filter.PropertyName,
                                        typeof(T).Name);
                                continue;
                        }

                        var propertyAccess = Expression.Property(parameter, property);
                        Expression? conditionExpression = null;

                        switch (filter.Operator)
                        {
                                case FilterOperator.Equals:
                                        conditionExpression = Expression.Equal(
                                                propertyAccess,
                                                Expression.Constant(filter.Value, property.PropertyType));
                                        break;

                                case FilterOperator.NotEquals:
                                        conditionExpression = Expression.NotEqual(
                                                propertyAccess,
                                                Expression.Constant(filter.Value, property.PropertyType));
                                        break;

                                case FilterOperator.Contains when property.PropertyType == typeof(string):
                                        var containsMethod = typeof(string).GetMethod("Contains", new[] { typeof(string) });
                                        conditionExpression = Expression.Call(
                                                propertyAccess,
                                                containsMethod!,
                                                Expression.Constant(filter.Value));
                                        break;

                                case FilterOperator.StartsWith when property.PropertyType == typeof(string):
                                        var startsWithMethod = typeof(string).GetMethod("StartsWith", new[] { typeof(string) });
                                        conditionExpression = Expression.Call(
                                                propertyAccess,
                                                startsWithMethod!,
                                                Expression.Constant(filter.Value));
                                        break;

                                case FilterOperator.EndsWith when property.PropertyType == typeof(string):
                                        var endsWithMethod = typeof(string).GetMethod("EndsWith", new[] { typeof(string) });
                                        conditionExpression = Expression.Call(
                                                propertyAccess,
                                                endsWithMethod!,
                                                Expression.Constant(filter.Value));
                                        break;

                                case FilterOperator.GreaterThan:
                                        conditionExpression = Expression.GreaterThan(
                                                propertyAccess,
                                                Expression.Constant(filter.Value, property.PropertyType));
                                        break;

                                case FilterOperator.LessThan:
                                        conditionExpression = Expression.LessThan(
                                                propertyAccess,
                                                Expression.Constant(filter.Value, property.PropertyType));
                                        break;

                                case FilterOperator.GreaterThanOrEqual:
                                        conditionExpression = Expression.GreaterThanOrEqual(
                                                propertyAccess,
                                                Expression.Constant(filter.Value, property.PropertyType));
                                        break;

                                case FilterOperator.LessThanOrEqual:
                                        conditionExpression = Expression.LessThanOrEqual(
                                                propertyAccess,
                                                Expression.Constant(filter.Value, property.PropertyType));
                                        break;

                                case FilterOperator.Between:
                                        if (filter.SecondValue == null)
                                        {
                                                _logger.LogWarning("Between operator requires SecondValue");
                                                continue;
                                        }
                                        var greaterThanOrEqual = Expression.GreaterThanOrEqual(
                                                propertyAccess,
                                                Expression.Constant(filter.Value, property.PropertyType));
                                        var lessThanOrEqual = Expression.LessThanOrEqual(
                                                propertyAccess,
                                                Expression.Constant(filter.SecondValue, property.PropertyType));
                                        conditionExpression = Expression.AndAlso(greaterThanOrEqual, lessThanOrEqual);
                                        break;

                                case FilterOperator.IsNull:
                                        conditionExpression = Expression.Equal(propertyAccess, Expression.Constant(null));
                                        break;

                                case FilterOperator.IsNotNull:
                                        conditionExpression = Expression.NotEqual(propertyAccess, Expression.Constant(null));
                                        break;
                        }

                        if (conditionExpression != null)
                        {
                                filterExpression = filterExpression == null
                                        ? conditionExpression
                                        : Expression.AndAlso(filterExpression, conditionExpression);
                        }
                }

                if (filterExpression != null)
                {
                        var lambda = Expression.Lambda<Func<T, bool>>(filterExpression, parameter);
                        query = query.Where(lambda);
                }

                return query;
        }

        /// <summary>
        /// تطبيق الترتيب
        /// </summary>
        protected virtual IQueryable<T> ApplyOrdering(
                IQueryable<T> query,
                string orderBy,
                bool ascending)
        {
                var property = typeof(T).GetProperty(orderBy);
                if (property == null)
                {
                        _logger.LogWarning(
                                "Property {PropertyName} not found on {EntityName}, using default ordering",
                                orderBy,
                                typeof(T).Name);
                        return query;
                }

                var parameter = Expression.Parameter(typeof(T), "x");
                var propertyAccess = Expression.Property(parameter, property);
                var lambda = Expression.Lambda(propertyAccess, parameter);

                var methodName = ascending ? "OrderBy" : "OrderByDescending";
                var orderByMethod = typeof(Queryable)
                        .GetMethods()
                        .First(m => m.Name == methodName && m.GetParameters().Length == 2)
                        .MakeGenericMethod(typeof(T), property.PropertyType);

                query = (IQueryable<T>)orderByMethod.Invoke(null, new object[] { query, lambda })!;

                return query;
        }

        // ====================================================================================
        // الإضافة
        // ====================================================================================

        public virtual async Task<T> AddAsync(
                T entity,
                CancellationToken cancellationToken = default)
        {
                _logger.LogDebug("Adding new {EntityName}", typeof(T).Name);

                // Only generate Id if not already set
                if (entity.Id == Guid.Empty)
                        entity.Id = Guid.NewGuid();
                entity.CreatedAt = DateTime.UtcNow;
                entity.IsDeleted = false;

                _dbSet.Add(entity);
                await _context.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Added {EntityName} with id {EntityId}", typeof(T).Name, entity.Id);

                return entity;
        }

        public virtual async Task<IEnumerable<T>> AddRangeAsync(
                IEnumerable<T> entities,
                CancellationToken cancellationToken = default)
        {
                var entityList = entities.ToList();

                _logger.LogDebug("Adding {Count} {EntityName} entities", entityList.Count, typeof(T).Name);

                foreach (var entity in entityList)
                {
                        // Only generate Id if not already set
                        if (entity.Id == Guid.Empty)
                                entity.Id = Guid.NewGuid();
                        entity.CreatedAt = DateTime.UtcNow;
                        entity.IsDeleted = false;
                }

                _dbSet.AddRange(entityList);
                await _context.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Added {Count} {EntityName} entities", entityList.Count, typeof(T).Name);

                return entityList;
        }

        // ====================================================================================
        // التحديث
        // ====================================================================================

        public virtual async Task UpdateAsync(
                T entity,
                CancellationToken cancellationToken = default)
        {
                _logger.LogDebug("Updating {EntityName} with id {EntityId}", typeof(T).Name, entity.Id);

                entity.UpdatedAt = DateTime.UtcNow;

                _context.Entry(entity).State = EntityState.Modified;

                // تجاهل بعض الخصائص
                _context.Entry(entity).Property(e => e.Id).IsModified = false;
                _context.Entry(entity).Property(e => e.CreatedAt).IsModified = false;
                _context.Entry(entity).Property(e => e.IsDeleted).IsModified = false;

                try
                {
                        await _context.SaveChangesAsync(cancellationToken);
                        _logger.LogInformation("Updated {EntityName} with id {EntityId}", typeof(T).Name, entity.Id);
                }
                catch (DbUpdateConcurrencyException ex)
                {
                        _logger.LogError(ex, "Concurrency error updating {EntityName} with id {EntityId}", typeof(T).Name, entity.Id);
                        throw;
                }
        }

        public virtual async Task PartialUpdateAsync(
                Guid id,
                Dictionary<string, object> updates,
                CancellationToken cancellationToken = default)
        {
                _logger.LogDebug("Partially updating {EntityName} with id {EntityId}", typeof(T).Name, id);

                var entity = await GetByIdTrackedAsync(id, false, cancellationToken);
                if (entity == null)
                {
                        _logger.LogWarning("{EntityName} with id {EntityId} not found", typeof(T).Name, id);
                        throw new KeyNotFoundException($"{typeof(T).Name} with id {id} not found");
                }

                foreach (var update in updates)
                {
                        var property = typeof(T).GetProperty(update.Key);
                        if (property != null &&
                                property.CanWrite &&
                                update.Key != nameof(IBaseEntity.Id) &&
                                update.Key != nameof(IBaseEntity.CreatedAt) &&
                                update.Key != nameof(IBaseEntity.IsDeleted))
                        {
                                property.SetValue(entity, update.Value);
                        }
                }

                entity.UpdatedAt = DateTime.UtcNow;

                try
                {
                        await _context.SaveChangesAsync(cancellationToken);
                        _logger.LogInformation("Partially updated {EntityName} with id {EntityId}", typeof(T).Name, id);
                }
                catch (DbUpdateConcurrencyException ex)
                {
                        _logger.LogError(ex, "Concurrency error partially updating {EntityName} with id {EntityId}", typeof(T).Name, id);
                        throw;
                }
        }

        // ====================================================================================
        // الحذف
        // ====================================================================================

        public virtual async Task DeleteAsync(
                T entity,
                CancellationToken cancellationToken = default)
        {
                _logger.LogDebug("Hard deleting {EntityName} with id {EntityId}", typeof(T).Name, entity.Id);

                var entry = _context.Entry(entity);
                if (entry.State == EntityState.Detached)
                {
                        _dbSet.Attach(entity);
                }

                _dbSet.Remove(entity);
                await _context.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Hard deleted {EntityName} with id {EntityId}", typeof(T).Name, entity.Id);
        }

        public virtual async Task DeleteAsync(
                Guid id,
                CancellationToken cancellationToken = default)
        {
                var entity = await GetByIdTrackedAsync(id, true, cancellationToken);
                if (entity != null)
                {
                        await DeleteAsync(entity, cancellationToken);
                }
                else
                {
                        _logger.LogWarning("{EntityName} with id {EntityId} not found for deletion", typeof(T).Name, id);
                }
        }

        public virtual async Task SoftDeleteAsync(
                T entity,
                CancellationToken cancellationToken = default)
        {
                _logger.LogDebug("Soft deleting {EntityName} with id {EntityId}", typeof(T).Name, entity.Id);

                var entry = _context.Entry(entity);
                if (entry.State == EntityState.Detached)
                {
                        _dbSet.Attach(entity);
                }

                entity.IsDeleted = true;
                entity.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Soft deleted {EntityName} with id {EntityId}", typeof(T).Name, entity.Id);
        }

        public virtual async Task SoftDeleteAsync(
                Guid id,
                CancellationToken cancellationToken = default)
        {
                var entity = await GetByIdTrackedAsync(id, false, cancellationToken);
                if (entity != null)
                {
                        await SoftDeleteAsync(entity, cancellationToken);
                }
                else
                {
                        _logger.LogWarning("{EntityName} with id {EntityId} not found for soft deletion", typeof(T).Name, id);
                }
        }

        public virtual async Task RestoreAsync(
                Guid id,
                CancellationToken cancellationToken = default)
        {
                _logger.LogDebug("Restoring {EntityName} with id {EntityId}", typeof(T).Name, id);

                var entity = await GetByIdTrackedAsync(id, true, cancellationToken);
                if (entity != null && entity.IsDeleted)
                {
                        entity.IsDeleted = false;
                        entity.UpdatedAt = DateTime.UtcNow;
                        await _context.SaveChangesAsync(cancellationToken);

                        _logger.LogInformation("Restored {EntityName} with id {EntityId}", typeof(T).Name, id);
                }
                else
                {
                        _logger.LogWarning("{EntityName} with id {EntityId} not found or not deleted", typeof(T).Name, id);
                }
        }

        public virtual async Task DeleteRangeAsync(
                IEnumerable<T> entities,
                bool softDelete = true,
                CancellationToken cancellationToken = default)
        {
                var entityList = entities.ToList();

                _logger.LogDebug(
                        "{DeleteType} deleting {Count} {EntityName} entities",
                        softDelete ? "Soft" : "Hard",
                        entityList.Count,
                        typeof(T).Name);

                foreach (var entity in entityList)
                {
                        var entry = _context.Entry(entity);
                        if (entry.State == EntityState.Detached)
                        {
                                _dbSet.Attach(entity);
                        }
                }

                if (softDelete)
                {
                        foreach (var entity in entityList)
                        {
                                entity.IsDeleted = true;
                                entity.UpdatedAt = DateTime.UtcNow;
                        }
                }
                else
                {
                        _dbSet.RemoveRange(entityList);
                }

                await _context.SaveChangesAsync(cancellationToken);

                _logger.LogInformation(
                        "{DeleteType} deleted {Count} {EntityName} entities",
                        softDelete ? "Soft" : "Hard",
                        entityList.Count,
                        typeof(T).Name);
        }

        // ====================================================================================
        // الإحصائيات والفحص
        // ====================================================================================

        public virtual async Task<int> CountAsync(
                Expression<Func<T, bool>>? predicate = null,
                bool includeDeleted = false,
                CancellationToken cancellationToken = default)
        {
                _logger.LogDebug("Counting {EntityName}", typeof(T).Name);

                var query = _dbSet.AsQueryable();
                query = ApplySoftDeleteFilter(query, includeDeleted);

                return predicate == null
                        ? await query.CountAsync(cancellationToken)
                        : await query.CountAsync(predicate, cancellationToken);
        }

        public virtual async Task<bool> ExistsAsync(
                Expression<Func<T, bool>> predicate,
                bool includeDeleted = false,
                CancellationToken cancellationToken = default)
        {
                _logger.LogDebug("Checking existence of {EntityName}", typeof(T).Name);

                var query = _dbSet.AsQueryable();
                query = ApplySoftDeleteFilter(query, includeDeleted);

                return await query.AnyAsync(predicate, cancellationToken);
        }
}
