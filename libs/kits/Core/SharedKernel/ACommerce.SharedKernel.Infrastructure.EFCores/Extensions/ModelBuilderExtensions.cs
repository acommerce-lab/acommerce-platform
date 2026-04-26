using Microsoft.EntityFrameworkCore;
using ACommerce.SharedKernel.Abstractions.Entities;
using System.Linq.Expressions;

namespace ACommerce.SharedKernel.Infrastructure.EFCore.Extensions;

/// <summary>
/// امتدادات ModelBuilder لتطبيق تكوينات مشتركة
/// </summary>
public static class ModelBuilderExtensions
{
	/// <summary>
	/// تطبيق تكوينات مشتركة لجميع الكيانات التي تطبق IBaseEntity
	/// </summary>
	public static ModelBuilder ApplyBaseEntityConfiguration(this ModelBuilder modelBuilder)
	{
		foreach (var entityType in modelBuilder.Model.GetEntityTypes())
		{
			// التحقق من أن الكيان يطبق IBaseEntity
			if (!typeof(IBaseEntity).IsAssignableFrom(entityType.ClrType))
				continue;

			// Index على CreatedAt
			modelBuilder.Entity(entityType.ClrType)
				.HasIndex(nameof(IBaseEntity.CreatedAt))
				.HasDatabaseName($"IX_{entityType.ClrType.Name}_CreatedAt");

			// Index على IsDeleted
			modelBuilder.Entity(entityType.ClrType)
				.HasIndex(nameof(IBaseEntity.IsDeleted))
				.HasDatabaseName($"IX_{entityType.ClrType.Name}_IsDeleted");

			// Global Query Filter لـ Soft Delete
			modelBuilder.Entity(entityType.ClrType)
				.HasQueryFilter(
					CreateSoftDeleteFilter(entityType.ClrType));
		}

		return modelBuilder;
	}

	/// <summary>
	/// إنشاء Query Filter للـ Soft Delete
	/// </summary>
	private static LambdaExpression CreateSoftDeleteFilter(Type entityType)
	{
		var parameter = System.Linq.Expressions.Expression.Parameter(entityType, "e");
		var property = System.Linq.Expressions.Expression.Property(
			parameter,
			nameof(IBaseEntity.IsDeleted));
		var condition = System.Linq.Expressions.Expression.Equal(
			property,
			System.Linq.Expressions.Expression.Constant(false));

		return System.Linq.Expressions.Expression.Lambda(condition, parameter);
	}

	/// <summary>
	/// تطبيق تكوين الأعمدة المشتركة
	/// </summary>
	public static ModelBuilder ApplyBaseEntityColumnConfiguration(this ModelBuilder modelBuilder)
	{
		foreach (var entityType in modelBuilder.Model.GetEntityTypes())
		{
			if (!typeof(IBaseEntity).IsAssignableFrom(entityType.ClrType))
				continue;

			// Id as Primary Key
			modelBuilder.Entity(entityType.ClrType)
				.HasKey(nameof(IBaseEntity.Id));

			// CreatedAt - Required
			modelBuilder.Entity(entityType.ClrType)
				.Property(nameof(IBaseEntity.CreatedAt))
				.IsRequired()
				.HasDefaultValueSql("GETUTCDATE()");

			// UpdatedAt - Nullable
			modelBuilder.Entity(entityType.ClrType)
				.Property(nameof(IBaseEntity.UpdatedAt))
				.IsRequired(false);

			// IsDeleted - Required with default false
			modelBuilder.Entity(entityType.ClrType)
				.Property(nameof(IBaseEntity.IsDeleted))
				.IsRequired()
				.HasDefaultValue(false);
		}

		return modelBuilder;
	}
}
